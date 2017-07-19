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
        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void ShouldSetTermAsRpcTermAndStayFollowerWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog());
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf);
            var state = follower.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void ShouldSetTermAsRpcTermAndStayFollowerWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog());
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf);
            var state = follower.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void ShouldSetTermAsRpcTermAndStayFollowerWhenReceivesAppendEntriesResponse(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog());
            var sendToSelf = new TestingSendToSelf();
            var follower = new Follower(currentState, sendToSelf);
            var state = follower.Handle(new AppendEntriesResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType<Follower>();
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 1, 2)]
        public void ShouldSetTermAsRpcTermAndStayFollowerWhenReceivesRequestVoteResponse(int currentTerm, int rpcTerm, int expectedTerm)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog());
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
        public void ShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog());
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf);
            var state = candidate.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void ShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog());
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf);
            var state = candidate.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void ShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntriesResponse(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog());
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf);
            var state = candidate.Handle(new AppendEntriesResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, typeof(Follower))]
        [InlineData(2, 1, 3, typeof(Candidate))]
        public void ShouldSetTermAsRpcTermAndStateWhenReceivesRequestVoteResponse(int currentTerm, int rpcTerm, int expectedTerm, Type expectedType)
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), currentTerm, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog());
            var sendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(currentState, sendToSelf);
            var state = candidate.Handle(new RequestVoteResponseBuilder().WithTerm(rpcTerm).Build());
            state.ShouldBeOfType(expectedType);
            state.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }
    }
}