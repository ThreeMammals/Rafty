using System.Diagnostics;
using System.Threading;
using Castle.Components.DictionaryAdapter;
using static Rafty.UnitTests.Wait;

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

    public class TestingNode : INode
    {
        
        public IState State { get; private set; }

        public int BecomeCandidateCount { get; private set; }

        public void SetState(IState state)
        {
            State = state;
        }

        public void BecomeLeader(CurrentState state)
        {
            throw new NotImplementedException();
        }

        public void BecomeFollower(CurrentState state)
        {
            throw new NotImplementedException();
        }

        public void BecomeCandidate(CurrentState state)
        {
            BecomeCandidateCount++;
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            return State.Handle(appendEntries);
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {
            return State.Handle(requestVote);
        }
    }

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
            _currentState = new CurrentState(Guid.NewGuid(), 0, default(Guid), -1, -1);
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

        [Fact]
        public void ShouldBecomeCandidateWhenFollowerReceivesTimeoutAndHasNotHeardFromLeader()
        {
            _node = new TestingNode();
            var node = (TestingNode)_node;
            node.SetState(new Follower(_currentState, _fsm, _log, _random, node, new SettingsBuilder().WithMinTimeout(0).WithMaxTimeout(0).Build()));
            var result = WaitFor(1000).Until(() => node.BecomeCandidateCount > 0);
            result.ShouldBeTrue();
        }

        [Fact]
        public void ShouldBecomeCandidateWhenFollowerReceivesTimeoutAndHasNotHeardFromLeaderSinceLastTimeout()
        {
            _node = new TestingNode();
            var node = (TestingNode)_node;
            node.SetState(new Follower(_currentState, _fsm, _log, _random, node, new SettingsBuilder().WithMinTimeout(0).WithMaxTimeout(0).Build()));
            _node.Handle(new AppendEntriesBuilder().WithTerm(1).WithLeaderCommitIndex(-1).Build());
            var result = WaitFor(1000).Until(() => node.BecomeCandidateCount > 0);
            result.ShouldBeTrue();
        }

        [Fact]
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

        [Fact]
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
}