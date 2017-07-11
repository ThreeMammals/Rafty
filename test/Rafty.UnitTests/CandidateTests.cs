using System;
using System.Collections.Generic;
using Rafty.Concensus;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    public class CandidateTests : IDisposable
    {
        private Node _node;
        private Guid _id;

        public CandidateTests()
        {
            _id = Guid.NewGuid();
            var currentState = new CurrentState(_id, new List<IPeer>(), 0, default(Guid));
            _node = new Node(currentState);
        }

        [Fact]
        public void ShouldIncrementCurrentTermWhenElectionStarts()
        {           
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.CurrentState.CurrentTerm.ShouldBe(1);
        }

        [Fact]
        public void ShouldVoteForSelfWhenElectionStarts()
        {           
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.CurrentState.VotedFor.ShouldBe(_id);
        }

        public void Dispose()
        {
            _node.Dispose();
        }
    }
}