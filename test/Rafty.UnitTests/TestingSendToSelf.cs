using System.Collections.Generic;
using Rafty.Concensus;

namespace Rafty.UnitTests
{
    public class TestingSendToSelf : ISendToSelf
    {
        private INode _node;

        public TestingSendToSelf()
        {
            Timeouts = new List<Timeout>();
            BeginElections = new List<BeginElection>();
        }

        public List<Timeout> Timeouts {get;private set;}
        public List<BeginElection> BeginElections {get;private set;}

        public void Dispose()
        {
        }

        public void Publish(Timeout timeout)
        {
            Timeouts.Add(timeout);
        }

        public void Publish(BeginElection beginElection)
        {
            BeginElections.Add(beginElection);
        }

        public void SetNode(INode node)
        {
            _node = node;
        }
    }
}