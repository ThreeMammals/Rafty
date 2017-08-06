using System.Threading;
using Castle.Components.DictionaryAdapter;

namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using Concensus;
    using Rafty.FiniteStateMachine;
    using Rafty.Log;
    using Shouldly;
    using Xunit;

/* Followers(�5.2):
� Respond to RPCs from candidates and leaders
� If election timeout elapses without receiving AppendEntries
RPC from current leader or granting vote to candidate:
convert to candidate
*/

    public class FollowerTests 
    {
        private readonly IFiniteStateMachine _fsm;
        private readonly List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _random;
        private INode _node;
        private CurrentState _currentState;

        public FollowerTests()
        {
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
        }

        [Fact]
        public void CommitIndexShouldBeInitialisedToMinusOne()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.CurrentState.CommitIndex.ShouldBe(-1);
        }

        [Fact]
        public void CurrentTermShouldBeInitialisedToZero()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.CurrentState.CurrentTerm.ShouldBe(0);
        }

        [Fact]
        public void LastAppliedShouldBeInitialisedToMinusOne()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.CurrentState.LastApplied.ShouldBe(-1);
        }

        [Fact(Skip = "cant implement this now")]
        public void ShouldBecomeCandidateWhenFollowerReceivesTimeoutAndHasNotHeardFromLeader()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.ShouldBeOfType<Follower>();
            Thread.Sleep(500);
            _node.State.ShouldBeOfType<Candidate>();
        }

        [Fact(Skip = "cant implement this at the moment")]
        public void ShouldBecomeCandidateWhenFollowerReceivesTimeoutAndHasNotHeardFromLeaderSinceLastTimeout()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntriesBuilder().WithTerm(1).WithLeaderCommitIndex(-1).Build());
            _node.State.ShouldBeOfType<Follower>();
            Thread.Sleep(500);
            _node.State.ShouldBeOfType<Candidate>();
        }

        [Fact(Skip = "This test is failing at the moment because it doesnt get to reset the election timer and become candidate")]
        public void ShouldNotBecomeCandidateWhenFollowerReceivesTimeoutAndHasHeardFromLeader()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntriesBuilder().WithTerm(1).WithLeaderCommitIndex(-1).Build());
            _node.State.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldNotBecomeCandidateWhenFollowerReceivesTimeoutAndHasHeardFromLeaderSinceLastTimeout()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntriesBuilder().WithTerm(1).WithLeaderCommitIndex(-1).Build());
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new AppendEntriesBuilder().WithTerm(1).WithLeaderCommitIndex(-1).Build());
            _node.State.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldStartAsFollower()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void VotedForShouldBeInitialisedToNone()
        {
            _node = new Node(_fsm, _log, _peers, _random, new SettingsBuilder().Build());
            _node.State.CurrentState.VotedFor.ShouldBe(default(Guid));
        }

        [Fact(Skip = "cant implement this at the moment")]
        public void ShouldUpdateVotedFor()
        {
            _node = new NothingNode();
            _currentState = new CurrentState(Guid.NewGuid(), 0, default(Guid), -1, -1);
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, new SettingsBuilder().Build());
            var requestVote = new RequestVoteBuilder().WithCandidateId(Guid.NewGuid()).Build();
            var requestVoteResponse = follower.Handle(requestVote);
            follower.CurrentState.VotedFor.ShouldBe(requestVote.CandidateId);
        }
    }

    public class NothingNode : INode
    {
        public IState State { get; }

        public int BecomeLeaderCount { get; private set; } 
        public int BecomeFollowerCount { get; private set; } 
        public int BecomeCandidateCount { get; private set; }

        public void BecomeLeader(CurrentState state)
        {
            BecomeLeaderCount++;
        }

        public void BecomeFollower(CurrentState state)
        {
            BecomeFollowerCount++;
        }

        public void BecomeCandidate(CurrentState state)
        {
            BecomeCandidateCount++;
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            return new AppendEntriesResponseBuilder().Build();
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {
            return new RequestVoteResponseBuilder().Build();
        }
    }
}