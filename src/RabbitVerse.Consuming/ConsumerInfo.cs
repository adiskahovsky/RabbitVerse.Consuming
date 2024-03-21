using System;

namespace RabbitVerse.Consuming
{
    public class ConsumerInfo
    {
        public ConsumerInfo(IConsumer consumer, string exchange, TimeSpan? retry)
        {
            Consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            Exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
            Retry = retry;
        }

        public IConsumer Consumer { get; }
        public TimeSpan? Retry { get; }
        public string Exchange { get; }

        public override int GetHashCode()
        {
            return Exchange.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var info = obj as ConsumerInfo;
            if (info == null)
                return false;

            return Exchange == info.Exchange;
        }
    }
}