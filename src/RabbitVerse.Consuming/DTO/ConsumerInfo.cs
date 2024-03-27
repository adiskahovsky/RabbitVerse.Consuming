using System;

namespace RabbitVerse.Consuming.DTO
{
    public class ConsumerInfo
    {
        public ConsumerInfo(IConsumer consumer, string exchange)
        {
            Consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            Exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        }

        public IConsumer Consumer { get; }
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