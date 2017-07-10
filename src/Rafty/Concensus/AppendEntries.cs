using System;

namespace Rafty.Concensus
{
    public sealed class AppendEntries
    {
        public AppendEntries(Guid id)
        {
            Id = id;
        }
        
        public Guid Id {get;private set;}
    }
}