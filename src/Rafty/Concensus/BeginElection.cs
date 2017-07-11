using System;

namespace Rafty.Concensus
{
    public class BeginElection : Message
    {
        public BeginElection() 
            : base(Guid.NewGuid())
        {
        }
    }
}