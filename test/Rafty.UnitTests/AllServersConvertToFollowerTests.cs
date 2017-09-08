using Xunit;
using TestStack.BDDfy;
using Shouldly;
using Rafty.Concensus;
using System;
using System.Collections.Generic;
using Rafty.Log;
using Rafty.FiniteStateMachine;
using Rafty.Concensus.States;

namespace Rafty.UnitTests
{
    public class AllServersConvertToFollowerTests
    {
/*All Servers:
• If commitIndex > lastApplied: increment lastApplied, apply
log[lastApplied] to state machine (§5.3)
• If RPC request or response contains term T > currentTerm:
set currentTerm = T, convert to follower (§5.1)*/

        private readonly IFiniteStateMachine _fsm;
        private readonly ILog _log;
        private List<IPeer> _peers;
        private readonly IRandomDelay _random;
        private readonly INode _node;
        private Settings _settings;
        private IRules _rules;

        public AllServersConvertToFollowerTests()
        {
            _rules = new Rules();
            _settings = new SettingsBuilder().Build();
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _node = new NothingNode();
        }

        //follower
        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(),currentTerm, default(Guid), 0, 0);
            var follower = new Follower(currentState, _fsm, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = follower.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            follower.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 0, 0);
            var follower = new Follower(currentState, _fsm, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = follower.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            follower.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        //candidate
        [Theory]
        [InlineData(0, 2, 2)]
        [InlineData(2, 3, 3)]
        public void CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 0, 0);
            var candidate = new Candidate(currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = candidate.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            candidate.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
            var node = (NothingNode)_node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        [Theory]
        [InlineData(0, 2, 2)]
        [InlineData(2, 3, 3)]
        public void CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 0, 0);
            var candidate = new Candidate(currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            var requestVoteResponse = candidate.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).WithLastLogIndex(1).Build());
            candidate.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
            var node = (NothingNode) _node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        //leader
        [Theory]
        [InlineData(0, 2, 2)]
        [InlineData(2, 3, 3)]
        public void LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm)
        {            
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 0, 0);

            var leader = new Leader(currentState, _fsm, _peers, _log, _node, _settings, _rules);
            var appendEntriesResponse = leader.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            leader.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2)]
        [InlineData(2, 3, 3)]
        public void LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 0, 0);
            var leader = new Leader(currentState, _fsm, _peers, _log, _node, _settings, _rules);
            var state = leader.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).WithLastLogIndex(1).Build());
            leader.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }
    }
}