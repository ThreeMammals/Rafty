using System;
using System.Collections.Generic;
using Rafty.Concensus;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    public class CandidateTests
    {
        private Node _node;

        public CandidateTests()
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0);
            _node = new Node(currentState);
        }

        [Fact]
        public void ShouldIncrementCurrentTermWhenElectionStarts()
        {           
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.CurrentState.CurrentTerm.ShouldBe(1);
        }
    }
}