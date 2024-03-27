using System;

namespace RabbitVerse.Consuming.DTO
{

    public class RetryInfo
    {
        public RetryInfo(int permonentCount, TimeSpan? infinityDelay, TimeSpan[]? onceDelays)
        {
            PermonentCount = permonentCount;
            InfinityDelay = infinityDelay;
            OnceDelays = onceDelays ?? Array.Empty<TimeSpan>();
        }

        public int PermonentCount { get; }
        public TimeSpan? InfinityDelay { get; }
        public TimeSpan[] OnceDelays { get; }
    }
}