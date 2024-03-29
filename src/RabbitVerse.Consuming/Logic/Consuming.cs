using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Collections.Concurrent;
using RabbitVerse.Consuming.Models.Contract.Provider;
using RabbitVerse.Consuming.DTO;
using RabbitVerse.Consuming.Logic.Models;
using RabbitVerse.Consuming.Contract;

namespace RabbitVerse.Consuming
{
    public class Consuming
    {
        private readonly string _name;
        private readonly ConnectionFactory _connectionFactory;

        private readonly IConsumingProviderFactory _factory;

        private readonly IEndpointsProvider _endpointsProvider;

        private readonly ConcurrentDictionary<string, (List<IRetry> onceRetries, InfinityRetry? infinityRetry)> _retriesRouting = new ConcurrentDictionary<string, (List<IRetry> onceRetries, InfinityRetry? infinityRetry)>();

        public Consuming(string name, ConnectionFactory connectionFactory, IConsumingProviderFactory factory, IEndpointsProvider endpointsProvider)
        {
            _name = name;
            _connectionFactory = connectionFactory;
            _factory = factory;
            _endpointsProvider = endpointsProvider;
        }

        public async Task Start(bool recreate)
        {
            using var connection = _connectionFactory.CreateConnection();
            var channel = connection.CreateModel();

            var endpoints = _endpointsProvider.GetEndpoinds();

            foreach (var endpoint in endpoints)
            {
                var exchangeName = endpoint.Exchange;
                var queueName = $"{_name}.{exchangeName}";

                if (recreate)
                {
                    channel.QueueDelete(queueName);
                    channel.ExchangeDelete(exchangeName);
                }

                var retries = CreateRetriesEntities(channel, recreate, endpoint, exchangeName, queueName);

                if (!_retriesRouting.TryAdd(exchangeName, retries))
                {
                    throw new Exception("Cannot setup retries");
                }

                channel.ExchangeDeclare(exchangeName, "fanout", true, false);
                channel.QueueDeclare(queueName, true, false, false);
                channel.QueueBind(queueName, exchangeName, string.Empty);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    var _ = Task.Run(async () =>
                    {
                        try
                        {
                            using (var scope = _factory.StartScope())
                            {

                                var body = ea.Body.ToArray();
                                var message = Encoding.UTF8.GetString(body);

                                var consumerInfo = scope.GetConsumerInfo(endpoint);
                                if (consumerInfo == null)
                                    throw new Exception($"No consumer info provided for: {endpoint.Exchange}");

                                var payload = JsonSerializer.Deserialize(message, consumerInfo.Consumer.PayloadType);

                                var result = await consumerInfo.Consumer.Handle(payload!);

                                if (result == ConsumerResult.Retry)
                                {
                                    (var retries, var infinity) = _retriesRouting.GetValueOrDefault(exchangeName);
                                    if (retries == null && infinity == null)
                                        throw new Exception("Cannot get retries info");

                                    ea.BasicProperties.Headers ??= new Dictionary<string, object>();

                                    var headers = ea.BasicProperties.Headers;
                                    if (!headers.TryGetValue("x-fail-count", out var failCountObj))
                                    {
                                        headers.Add("x-fail-count", 1);
                                    }

                                    var failCount = (failCountObj as int?) ?? 1;
                                    headers["x-fail-count"] = ++failCount;

                                    if (retries?.Count >= failCount)
                                    {
                                        var retry = retries[failCount - 1];

                                        switch (retry.Type)
                                        {
                                            case RetryType.Permonent:
                                                channel.BasicPublish(exchangeName, "", ea.BasicProperties, body);
                                                break;
                                            case RetryType.PeriodOnce:
                                                var retryObj = (OnceRetry)retry;
                                                channel.BasicPublish(retryObj.Exchnage, "", false, ea.BasicProperties, body);
                                                break;
                                        }

                                    }
                                    else if (infinity != null)
                                    {
                                        channel.BasicPublish(infinity.Exchnage, "", false, ea.BasicProperties, body);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            channel.BasicAck(ea.DeliveryTag, false);
                        }
                    });
                };

                channel.BasicConsume(queueName, false, consumer);
            }

            var completionSource = new TaskCompletionSource<bool>();
            await completionSource.Task;
        }

        private (List<IRetry>, InfinityRetry?) CreateRetriesEntities(IModel channel, bool recreate, Endpoint endpoint, string mainExchangeName, string mainQueueName)
        {
            var duplicateTimes = new Dictionary<TimeSpan, int>();

            var results = new List<IRetry>(endpoint.Retries.PermonentCount + endpoint.Retries.OnceDelays.Length + (endpoint.Retries.InfinityDelay == null ? 0 : 1));

            for (var permonentCounter = 1; permonentCounter <= endpoint.Retries.PermonentCount; permonentCounter++)
            {
                results.Add(new PermonentRetry());
            }

            foreach (var retry in endpoint.Retries.OnceDelays)
            {
                int duplicateCounter = 1;
                if (duplicateTimes.ContainsKey(retry))
                {
                    duplicateCounter = ++duplicateTimes[retry];
                }
                else
                {
                    duplicateTimes.Add(retry, duplicateCounter);
                }

                var dlxExchange = $"dlx.{retry}.X{duplicateCounter}.{mainExchangeName}";
                var dlxQueue = $"dlx.{retry}.X{duplicateCounter}.{mainQueueName}";

                var dleArgs = new Dictionary<string, object>
                {
                    { "x-message-ttl", (int)retry.TotalMilliseconds },
                    { "x-dead-letter-exchange", mainExchangeName }
                };

                if (recreate)
                {
                    channel.ExchangeDelete(dlxExchange);
                    channel.QueueDelete(dlxQueue);
                }

                channel.ExchangeDeclare(dlxExchange, "fanout", true, false);
                channel.QueueDeclare(dlxQueue, true, false, false, dleArgs);
                channel.QueueBind(dlxQueue, dlxExchange, "");

                results.Add(new OnceRetry(dlxExchange));
            }

            InfinityRetry? infinityRetry = null;
            if (endpoint.Retries.InfinityDelay != null)
            {
                var dlxExchange = $"dlx.{endpoint.Retries.InfinityDelay}.X.{mainExchangeName}";
                var dlxQueue = $"dlx.{endpoint.Retries.InfinityDelay}.X.{mainQueueName}";
                var dleArgs = new Dictionary<string, object>
                {
                    { "x-message-ttl", (int)endpoint.Retries.InfinityDelay.Value.TotalMilliseconds },
                    { "x-dead-letter-exchange", mainExchangeName }
                };

                if (recreate)
                {
                    channel.ExchangeDelete(dlxExchange);
                    channel.QueueDelete(dlxQueue);
                }

                channel.ExchangeDeclare(dlxExchange, "fanout", true, false);
                channel.QueueDeclare(dlxQueue, true, false, false, dleArgs);
                channel.QueueBind(dlxQueue, dlxExchange, "");
                infinityRetry = new InfinityRetry(dlxExchange);
            }

            return (results, infinityRetry);
        }
    }
}