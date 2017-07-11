using System;

namespace Rafty.Concensus
{
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
            if(_delay == null)
            {
                _delay = TimeSpan.FromMilliseconds(0);
            }

            return new Timeout(_delay);
        }
    }
    
    public class Timeout : Message
    {
        public Timeout(TimeSpan delay)
            :base(Guid.NewGuid())
        {
            this.Delay = delay;
        }

        public TimeSpan Delay { get; private set; }
    }
}