/*namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using Concensus;

    public class TestingSendToSelf : ISendToSelf
    {
        private INode _node;

        public TestingSendToSelf()
        {
            Timeouts = new List<Timeout>();
            BeginElections = new List<BeginElection>();
        }

        public List<Timeout> Timeouts { get; }
        public List<BeginElection> BeginElections { get; }

        public void Dispose()
        {
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }

        public void Publish(Timeout timeout)
        {
            Timeouts.Add(timeout);
        }

        public void Publish(BeginElection beginElection)
        {
            BeginElections.Add(beginElection);
        }

        public void Restart()
        {
            throw new NotImplementedException();
        }

        public void SetNode(INode node)
        {
            _node = node;
        }
    }
}*/