namespace Rafty.Concensus
{
    using System;
    public interface IRandomDelay
    {
        TimeSpan Get(int leastMilliseconds, int maxMilliseconds);
    }
}