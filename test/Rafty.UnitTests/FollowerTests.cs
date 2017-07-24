namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using Concensus;
    using Rafty.Log;
    using Shouldly;
    using Xunit;

/* Followers(�5.2):
� Respond to RPCs from candidates and leaders
� If election timeout elapses without receiving AppendEntries
RPC from current leader or granting vote to candidate:
convert to candidate
*/

    public class FollowerTests : IDisposable
    {
        public FollowerTests()
        {
            _sendToSelf = new SendToSelf();
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), 0, 0);
            _node = new Node(_currentState, _sendToSelf);
            _sendToSelf.SetNode(_node);
        }

        public void Dispose()
        {
            _node.Dispose();
        }

        private readonly Node _node;
        private ISendToSelf _sendToSelf;
        private readonly CurrentState _currentState;

        [Fact]
        public void CommitIndexShouldBeInitialisedToZero()
        {
            _node.State.CurrentState.CommitIndex.ShouldBe(0);
        }

        [Fact]
        public void CurrentTermShouldBeInitialisedToZero()
        {
            _node.State.CurrentState.CurrentTerm.ShouldBe(0);
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
        public void ShouldStartAsFollower()
        {
            _node.State.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void VotedForShouldBeInitialisedToNone()
        {
            _node.State.CurrentState.VotedFor.ShouldBe(default(Guid));
        }

        [Fact]
        public void ShouldUpdateVotedFor()
        {
            _sendToSelf = new TestingSendToSelf();
            var follower = new Follower(_currentState, _sendToSelf);
            var requestVote = new RequestVoteBuilder().WithCandidateId(Guid.NewGuid()).Build();
            var state = follower.Handle(requestVote);
            state.CurrentState.VotedFor.ShouldBe(requestVote.CandidateId);
        }
    }
}