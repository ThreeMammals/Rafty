using System;
using System.Collections.Generic;
using Rafty.Concensus;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    public class CandidateTests : IDisposable
    {
        private readonly Node _node;
        private readonly Guid _id;
        private ISendToSelf _sendToSelf;

        public CandidateTests()
        {
            _id = Guid.NewGuid();
            var currentState = new CurrentState(_id, new List<IPeer>(), 0, default(Guid));
            _sendToSelf = new SendToSelf();
            _node = new Node(currentState, _sendToSelf);
            _sendToSelf.SetNode(_node);
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

        [Fact]
        public void ShouldResetElectionTimerWhenElectionStarts()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _node.Dispose();
        }
    }
}