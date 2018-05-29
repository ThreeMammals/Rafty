namespace Rafty.Infrastructure
{
    using System;

    public interface IRandomDelay
    {
        TimeSpan Get(int leastMilliseconds, int maxMilliseconds);
    }
}