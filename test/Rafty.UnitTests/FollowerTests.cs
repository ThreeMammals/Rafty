using System;
using System.Collections.Generic;
using Moq;
using Shouldly;
using Xunit;
using Rafty.Concensus;
using System.Threading;

namespace Rafty.UnitTests
{
    public class FollowerTests : IDisposable
    {
        private readonly Node _node;
        private ISendToSelf _sendToSelf;

        public FollowerTests()
        {
            _sendToSelf = new SendToSelf();
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid));
            _node = new Node(currentState, _sendToSelf);
            _sendToSelf.SetNode(_node);
        }
        
        [Fact]
        public void ShouldStartAsFollower()
        {            
            _node.State.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void CurrentTermShouldBeInitialisedToZero()
        {
            _node.State.CurrentState.CurrentTerm.ShouldBe(0);
        }

        [Fact]
        public void VotedForShouldBeInitialisedToNone()
        {
            _node.State.CurrentState.VotedFor.ShouldBe(default(Guid));
        }

        [Fact]
        public void CommitIndexShouldBeInitialisedToZero()
        {
            _node.State.CurrentState.CommitIndex.ShouldBe(0);
        }

        [Fact]
        public void LastAppliedShouldBeInitialisedToZero()
        {
            _node.State.CurrentState.LastApplied.ShouldBe(0);
        }

        [Fact]
        public void ShouldBecomeCandidateWhenFollowerReceivesTimeoutAndHasNotHeardFromLeader()
        {           
             _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.ShouldBeOfType<Candidate>();

            //todo is this the best way to do messaging at integration level?
            // using(var timeOutMessenger = new SendToSelf(_node))
            // {
            //     timeOutMessenger.Publish(new Concensus.Timeout(Guid.NewGuid(), TimeSpan.FromMilliseconds(0)));
            //     Thread.Sleep(100);
            //     _node.State.ShouldBeOfType<Candidate>();
            // }
        }

        [Fact]
        public void ShouldIncrementCurrentTermWhenElectionStarts()
        {           
             _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.ShouldBeOfType<Candidate>();
        }

        [Fact]
        public void ShouldNotBecomeCandidateWhenFollowerReceivesTimeoutAndHasHeardFromLeader()
        {
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntriesBuilder().Build());
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldNotBecomeCandidateWhenFollowerReceivesTimeoutAndHasHeardFromLeaderSinceLastTimeout()
        {
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntriesBuilder().Build());
            _node.Handle(new TimeoutBuilder().Build());
            _node.Handle(new AppendEntriesBuilder().Build());
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldBecomeCandidateWhenFollowerReceivesTimeoutAndHasNotHeardFromLeaderSinceLastTimeout()
        {
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntriesBuilder().Build());
            _node.Handle(new TimeoutBuilder().Build());
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.ShouldBeOfType<Candidate>();
        }

        public void Dispose()
        {
            _node.Dispose();
        }
    }
}