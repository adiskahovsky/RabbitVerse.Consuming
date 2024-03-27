using System;

namespace RabbitVerse.Consuming.Logic.Models
{
    internal class InfinityRetry : IRetry
    {
        public RetryType Type => RetryType.PeriodInfinity;

        public string Exchnage { get; }

        public InfinityRetry(string exchnage)
        {
            Exchnage = exchnage ?? throw new ArgumentNullException(nameof(exchnage));
        }
    }

}