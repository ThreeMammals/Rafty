using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rafty.Concensus;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    public class CandidateTests : IDisposable
    {
        private Node _node;
        private readonly Guid _id;
        private SendToSelf _sendToSelf;
        private CurrentState _currentState;

        public CandidateTests()
        {
            _id = Guid.NewGuid();
            _currentState = new CurrentState(_id, new List<IPeer>(), 0, default(Guid), TimeSpan.FromMilliseconds(0));
            _sendToSelf = new SendToSelf();
            _node = new Node(_currentState, _sendToSelf);
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
        public async Task ShouldResetTimeoutWhenElectionStarts()
        {         
            var testingSendToSelf = new TestingSendToSelf(_sendToSelf); 
            _node = new Node(_currentState, testingSendToSelf);
            testingSendToSelf.SetNode(_node);
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new TimeoutBuilder().Build());
            //this is kinda lame but best way to make sure this works end to end?
            await Task.Delay(10);
            testingSendToSelf.Timeouts.Count.ShouldBe(1);
        }

        public void Dispose()
        {
            _node.Dispose();
        }
    }
}