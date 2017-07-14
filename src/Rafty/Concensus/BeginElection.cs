namespace Rafty.Concensus
{
    using System;

    public class BeginElection : Message
    {
        public BeginElection()
            : base(Guid.NewGuid())
        {
        }
    }
}