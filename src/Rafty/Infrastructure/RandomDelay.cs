namespace Rafty.Concensus
{
    using System;
    public class RandomDelay : IRandomDelay
    {
        private Random _random;

        public RandomDelay()
        {
            _random = new Random(Guid.NewGuid().GetHashCode());
        }
        public TimeSpan Get(int leastMilliseconds, int maxMilliseconds)
        {
            var randomMs = _random.Next(leastMilliseconds, maxMilliseconds);
            return TimeSpan.FromMilliseconds(randomMs);       
        }
    }
}