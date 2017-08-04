using Xunit;
using TestStack.BDDfy;
using Shouldly;
using Rafty.Concensus;
using System;
using System.Collections.Generic;
using Rafty.Log;
using Rafty.FiniteStateMachine;

namespace Rafty.UnitTests
{
    public class AllServersConvertToFollowerTests
    {
/*All Servers:
• If commitIndex > lastApplied: increment lastApplied, apply
log[lastApplied] to state machine (§5.3)
• If RPC request or response contains term T > currentTerm:
set currentTerm = T, convert to follower (§5.1)*/

        private IFiniteStateMachine _fsm;
        private ILog _log;
        private List<IPeer> _peers;
        private IRandomDelay _random;
        
        public AllServersConvertToFollowerTests()
        {
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
        }

        //follower
        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(),currentTerm, default(Guid), 
                TimeSpan.FromSeconds(0), 0, 0);
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf, _fsm, _peers, _log, _random);
            var state = follower.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 
                TimeSpan.FromSeconds(0), 0, 0);
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf, _fsm, _peers, _log, _random);
            var state = follower.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        //candidate
        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 
                TimeSpan.FromSeconds(0), 0, 0);
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf, _fsm, _peers, _log, _random);
            var state = candidate.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 
                TimeSpan.FromSeconds(0), 0, 0);
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf, _fsm, _peers, _log, _random);
            var state = candidate.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        //leader
        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 2, typeof(Leader))]
        public void LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 
                TimeSpan.FromSeconds(0), 0, 0);
            var sendToSelf = new TestingSendToSelf();
            var leader = new Leader(currentState, sendToSelf, _fsm, _peers, _log, _random);
            var state = leader.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 2, typeof(Leader))]
        public void LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), currentTerm, default(Guid), 
                TimeSpan.FromSeconds(0), 0, 0);
            var sendToSelf = new TestingSendToSelf();
            var leader = new Leader(currentState, sendToSelf, _fsm, _peers, _log, _random);
            var state = leader.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }
    }
}