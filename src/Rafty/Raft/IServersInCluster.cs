using System;
using System.Collections.Generic;
using System.Linq;
using Rafty.ServiceDiscovery;

namespace Rafty.Raft
{
    public interface IServersInCluster
    {
        int Count { get; }
        void Add(ServerInCluster serverInCluster);
        void Add(List<ServerInCluster> serverInCluster);
        void Remove(ServerInCluster serverInCluster);
        bool Contains(Guid id);
        List<ServerInCluster> Get(Func<ServerInCluster, bool> predicate);
    }

    public class InMemoryServersInCluster : IServersInCluster
    {
        public InMemoryServersInCluster()
        {
            All = new List<ServerInCluster>();
        }

        public InMemoryServersInCluster(List<ServerInCluster> remoteServers)
        {
            All = remoteServers ?? new List<ServerInCluster>();
        }

        public int Count => All.Count;
        public List<ServerInCluster> All { get; }


        public void Add(ServerInCluster serverInCluster)
        {
            All.Add(serverInCluster);
        }

        public void Add(List<ServerInCluster> serverInCluster)
        {
            All.AddRange(serverInCluster);
        }

        public void Remove(ServerInCluster serverInCluster)
        {
            All.Remove(serverInCluster);
        }

        public bool Contains(Guid id)
        {
            return All.Select(x => x.Id).Contains(id);
        }

        public List<ServerInCluster> Get(Func<ServerInCluster, bool> predicate)
        {
            return All.Where(predicate).ToList();
        }
    }
}
