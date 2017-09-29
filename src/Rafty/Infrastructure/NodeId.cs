using System;

namespace Rafty.Infrastructure
{
    public class NodeId
    {
        public NodeId(Guid id)
        {
            Id = id;
        }

        public Guid Id {get;private set;}
    }
}
