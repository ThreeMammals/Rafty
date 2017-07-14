namespace Rafty.Concensus
{
    using System;

    public class TimeoutBuilder
    {
        private TimeSpan _delay;

        public TimeoutBuilder WithDelay(int milliSeconds)
        {
            _delay = TimeSpan.FromMilliseconds(milliSeconds);
            return this;
        }

        public Timeout Build()
        {
            return new Timeout(_delay);
        }
    }

    public class Timeout : Message
    {
        public Timeout(TimeSpan delay)
            : base(Guid.NewGuid(), delay)
        {
        }
    }
}