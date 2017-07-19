using Xunit;
using TestStack.BDDfy;
using Shouldly;
using Rafty.Concensus;
using System;
using System.Collections.Generic;
using Rafty.Log;

namespace Rafty.UnitTests
{
    public class AllServersTests
    {
/*All Servers:
• If commitIndex > lastApplied: increment lastApplied, apply
log[lastApplied] to state machine (§5.3)
• If RPC request or response contains term T > currentTerm:
set currentTerm = T, convert to follower (§5.1)*/

        //follower

        // [Fact]
        // public void FollowerShouldIncrementLastAppliedAndApplyLogLastApplied()
        // {
        //     var log = new InMemoryLog();
        //     var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 1, default(Guid), TimeSpan.FromSeconds(0), log, 0);
        //     var sendToSelf = new TestingSendToSelf();
        //     var follower = new Follower(currentState, sendToSelf);
        //     var state = follower.Handle(new AppendEntriesBuilder().WithTerm(1).WithLeaderCommitIndex(1).Build());
        //     state.ShouldBeOfType<Follower>();
        //     state.CurrentState.CommitIndex.ShouldBe(1);
        //     log.LastLogIndex.ShouldBe(1);
        // }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf);
            var state = follower.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf);
            var state = follower.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesAppendEntriesResponse(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf);
            var state = follower.Handle(new AppendEntriesResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesRequestVoteResponse(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf);
            var state = follower.Handle(new RequestVoteResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        //candidate
        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf);
            var state = candidate.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf);
            var state = candidate.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntriesResponse(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf);
            var state = candidate.Handle(new AppendEntriesResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void CandidateShouldSetTermAsRpcTermAndStateWhenReceivesRequestVoteResponse(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf);
            var state = candidate.Handle(new RequestVoteResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        //leader
        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 2, typeof(Leader))]
        public void LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var leader = new Leader(currentState, sendToSelf);
            var state = leader.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 2, typeof(Leader))]
        public void LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var leader = new Leader(currentState, sendToSelf);
            var state = leader.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 2, typeof(Leader))]
        public void LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntriesResponse(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var leader = new Leader(currentState, sendToSelf);
            var state = leader.Handle(new AppendEntriesResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 2, typeof(Leader))]
        public void LeaderShouldSetTermAsRpcTermAndStateWhenReceivesRequestVoteResponse(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), 0);
            var sendToSelf = new TestingSendToSelf();
            var leader = new Leader(currentState, sendToSelf);
            var state = leader.Handle(new RequestVoteResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }
    }
}