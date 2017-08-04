namespace Rafty.Concensus
{
    using System;

    public class Timeout : Message
    {
        public Timeout()
            : base(Guid.NewGuid())
        {
        }

        public Timeout(TimeSpan delay)
            : base(Guid.NewGuid(), delay)
        {
        }
    }
}