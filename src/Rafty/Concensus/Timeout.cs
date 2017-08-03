namespace Rafty.Concensus
{
    using System;

    public class Timeout : Message
    {
        public Timeout(TimeSpan delay)
            : base(Guid.NewGuid(), delay)
        {
        }
    }
}