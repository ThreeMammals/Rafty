using System;

namespace Rafty
{
    public class ServerInCluster
    {
        public ServerInCluster(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; private set; }
    }
}