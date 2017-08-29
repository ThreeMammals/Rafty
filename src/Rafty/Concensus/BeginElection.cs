namespace Rafty.Concensus
{
    using System;

    public class BeginElection : Message
    {
        public BeginElection()
            : base(Guid.NewGuid())
        {
        }

         public BeginElection(TimeSpan timeSpan)
            : base(Guid.NewGuid(), timeSpan)
        {
        }
    }
}