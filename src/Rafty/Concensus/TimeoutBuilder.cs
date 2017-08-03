namespace Rafty.Concensus
{
    using System;
    public class TimeoutBuilder
    {
        private TimeSpan _delay;

        public TimeoutBuilder WithDelay(TimeSpan delay)
        {
            _delay = delay;
            return this;
        }

        public Timeout Build()
        {
            return new Timeout(_delay);
        }
    }
}