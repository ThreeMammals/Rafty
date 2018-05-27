using System.Collections.Generic;
using Rafty.Concensus;
using Rafty.Infrastructure;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    using Concensus.Peers;

    public class PeersProviderTests
    {
        [Fact]
        public void ShouldReturnPeers()
        {
            var input = new List<IPeer>
            {
                new FakePeer(),
                new FakePeer(),
                new FakePeer(),
                new FakePeer(),
                new FakePeer()
            };
            var provider = new InMemoryPeersProvider(input);
            var peers = provider.Get();
            peers.Count.ShouldBe(5);
        }
    }
}