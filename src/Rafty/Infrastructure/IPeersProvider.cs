using System.Collections.Generic;
using Rafty.Concensus;

namespace Rafty.Infrastructure
{
    using Concensus.Peers;

    public interface IPeersProvider
    {
        /// <summary>
        /// This will return all available peers. If a peer goes down it should not be returned.
        /// </summary>
        List<IPeer> Get();
    }
}