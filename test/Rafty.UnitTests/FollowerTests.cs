using System;
using System.Collections.Generic;
using Moq;
using Shouldly;
using Xunit;
using Rafty.Concensus;
using System.Threading;

namespace Rafty.UnitTests
{
    public class FollowerTests
    {
        private Node _node;

        public FollowerTests()
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>());
            _node = new Node(currentState);
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
            _node.Handle(new Concensus.Timeout(Guid.NewGuid(), TimeSpan.FromMilliseconds(0)));
            _node.State.ShouldBeOfType<Candidate>();

            //todo is this the best way to do messaging at integration level?
            // using(var timeOutMessenger = new TimeoutMessager(_node))
            // {
            //     timeOutMessenger.Publish(new Concensus.Timeout(Guid.NewGuid(), TimeSpan.FromMilliseconds(0)));
            //     Thread.Sleep(100);
            //     _node.State.ShouldBeOfType<Candidate>();
            // }
        }

        [Fact]
        public void ShouldNotBecomeCandidateWhenFollowerReceivesTimeoutAndHasHeardFromLeader()
        {
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntries(Guid.NewGuid()));
            _node.Handle(new Concensus.Timeout(Guid.NewGuid(), TimeSpan.FromMilliseconds(0)));
            _node.State.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldNotBecomeCandidateWhenFollowerReceivesTimeoutAndHasHeardFromLeaderSinceLastTimeout()
        {
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntries(Guid.NewGuid()));
            _node.Handle(new Concensus.Timeout(Guid.NewGuid(), TimeSpan.FromMilliseconds(0)));
            _node.Handle(new AppendEntries(Guid.NewGuid()));
            _node.Handle(new Concensus.Timeout(Guid.NewGuid(), TimeSpan.FromMilliseconds(0)));
            _node.State.ShouldBeOfType<Follower>();
        }

          [Fact]
        public void ShouldBecomeCandidateWhenFollowerReceivesTimeoutAndHasNotHeardFromLeaderSinceLastTimeout()
        {
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntries(Guid.NewGuid()));
            _node.Handle(new Concensus.Timeout(Guid.NewGuid(), TimeSpan.FromMilliseconds(0)));
            _node.Handle(new Concensus.Timeout(Guid.NewGuid(), TimeSpan.FromMilliseconds(0)));
            _node.State.ShouldBeOfType<Candidate>();
        }
    }
}