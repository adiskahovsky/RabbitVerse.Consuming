using System;

namespace RabbitVerse.Consuming.Logic.Models
{
    internal class OnceRetry : IRetry
    {
        public RetryType Type => RetryType.PeriodOnce;

        public string Exchnage { get; }

        public OnceRetry(string exchnage)
        {
            Exchnage = exchnage ?? throw new ArgumentNullException(nameof(exchnage));
        }
    }

}