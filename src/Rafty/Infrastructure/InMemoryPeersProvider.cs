using System.Collections.Generic;
using Rafty.Concensus;

namespace Rafty.Infrastructure
{
    using Concensus.Peers;

    public class InMemoryPeersProvider : IPeersProvider
    {   
        private List<IPeer> _peers;

        public InMemoryPeersProvider(List<IPeer> peers)
        {
            _peers = peers;
        }
        
        public List<IPeer> Get()
        {
            return _peers;
        }
    }
}