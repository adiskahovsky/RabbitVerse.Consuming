using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RabbitVerse.Consuming
{
    public class Consuming
    {
        private readonly string _name;
        private readonly ConnectionFactory _connectionFactory;

        private readonly IConsumerProvider _consumerProvider;

        public Consuming(string name, ConnectionFactory connectionFactory, IConsumerProvider consumerProvider)
        {
            _name = name;
            _connectionFactory = connectionFactory;
            _consumerProvider = consumerProvider;
        }

        public async Task Start(bool recreate)
        {
            using var connection = _connectionFactory.CreateConnection();
            var channel = connection.CreateModel();

            var routing = _consumerProvider.Get();

            foreach (var item in routing)
            {
                var exchangeName = item.Exchange;
                var queueName = $"{_name}.{exchangeName}";

                var dleArgs = new Dictionary<string, object>();

                if (item.Retry != null)
                    dleArgs.Add("x-message-ttl", (int)item.Retry.Value.TotalMilliseconds); // 1 минута
                dleArgs.Add("x-dead-letter-exchange", exchangeName);

                var dleExchange = $"dle.{exchangeName}";
                var dleQueueName = $"dle.{queueName}";

                if (recreate)
                {
                    channel.ExchangeDelete(dleExchange);
                    channel.QueueDelete(dleQueueName);
                    channel.QueueDelete(queueName);
                    channel.ExchangeDelete(exchangeName);
                }
                
                channel.ExchangeDeclare(dleExchange, "fanout", true, false);
                channel.QueueDeclare(dleQueueName, true, false, false, dleArgs);
                channel.QueueBind(dleQueueName, dleExchange, "");

                var args = new Dictionary<string, object>();
                args.Add("x-dead-letter-exchange", dleExchange);

                channel.ExchangeDeclare(exchangeName, "fanout", true, false);
                channel.QueueDeclare(queueName, true, false, false, args);
                channel.QueueBind(queueName, exchangeName, string.Empty);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    Task.Run(async () =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        var consumerInfo = item;

                        var payload = JsonSerializer.Deserialize(message, consumerInfo.Consumer.PayloadType);

                        var result = await consumerInfo.Consumer.Handle(payload!);

                        if (result == ConsumerResult.Accept)
                            channel.BasicAck(ea.DeliveryTag, false);

                        else if (result == ConsumerResult.Retry)
                            channel.BasicReject(ea.DeliveryTag, consumerInfo.Retry == null);
                    });
                };

                channel.BasicConsume(queueName, false, consumer);
            } 

            var completionSource = new TaskCompletionSource<bool>();
            await completionSource.Task;
        }
    }
}