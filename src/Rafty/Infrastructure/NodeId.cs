using System;

namespace Rafty.Infrastructure
{
    public class NodeId
    {
        public NodeId(string id)
        {
            Id = id;
        }

        public string Id {get;private set;}
    }
}
