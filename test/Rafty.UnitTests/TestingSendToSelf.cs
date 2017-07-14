using System.Collections.Generic;
using Rafty.Concensus;

namespace Rafty.UnitTests
{
    public class TestingSendToSelf : ISendToSelf
    {
        private ISendToSelf _sendToSelf;

        public TestingSendToSelf(ISendToSelf sendToSelf)
        {
            _sendToSelf = sendToSelf;
            Timeouts = new List<Timeout>();
            BeginElections = new List<BeginElection>();
        }

        public List<Timeout> Timeouts {get;private set;}
        public List<BeginElection> BeginElections {get;private set;}

        public void Dispose()
        {
            _sendToSelf.Dispose();
        }

        public void Publish(Timeout timeout)
        {
            Timeouts.Add(timeout);
            _sendToSelf.Publish(timeout);
        }

        public void Publish(BeginElection beginElection)
        {
            BeginElections.Add(beginElection);
            _sendToSelf.Publish(beginElection);
        }

        public void SetNode(INode node)
        {
            _sendToSelf.SetNode(node);
        }
    }
}