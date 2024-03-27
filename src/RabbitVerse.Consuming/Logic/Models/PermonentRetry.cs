namespace RabbitVerse.Consuming.Logic.Models
{
    internal class PermonentRetry : IRetry
    {
        public RetryType Type => RetryType.Permonent;
    }

}