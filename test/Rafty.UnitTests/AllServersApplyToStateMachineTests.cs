using Xunit;
using TestStack.BDDfy;
using Shouldly;
using Rafty.Concensus;
using System;
using System.Collections.Generic;
using Rafty.Log;

namespace Rafty.UnitTests
{
    public class AllServersApplyToStateMachineTests
    {
/*
• If commitIndex > lastApplied: increment lastApplied, apply log[lastApplied] to state machine (§5.3)\
*/

        [Fact] 
        public void FollowerShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), -1, -1);
            var sendToSelf = new TestingSendToSelf();
            var fsm = new InMemoryStateMachine();
            var follower = new Follower(currentState, sendToSelf, fsm);
            var log = new LogEntry("test", typeof(string), 1, 0);
            var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithEntry(log)
                .Build();
            //assume node has added the log..
            currentState.Log.Apply(log);
            var state = follower.Handle(appendEntries);
            state.State.ShouldBeOfType<Follower>();
            state.State.CurrentState.CurrentTerm.ShouldBe(1);
            state.State.CurrentState.LastApplied.ShouldBe(0);
            fsm.ExposedForTesting.ShouldBe(1);
        }

         [Fact] 
        public void CandidateShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), -1, -1);
            var sendToSelf = new TestingSendToSelf();
            var fsm = new InMemoryStateMachine();
            var follower = new Candidate(currentState, sendToSelf, fsm);
            var log = new LogEntry("test", typeof(string), 1, 0);
            var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithEntry(log)
                .Build();
            //assume node has added the log..
            currentState.Log.Apply(log);
            var state = follower.Handle(appendEntries);
            state.State.ShouldBeOfType<Candidate>();
            state.State.CurrentState.CurrentTerm.ShouldBe(1);
            state.State.CurrentState.LastApplied.ShouldBe(0);
            fsm.ExposedForTesting.ShouldBe(1);
        }


         [Fact] 
        public void LeaderShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 1, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), -1, -1);
            var sendToSelf = new TestingSendToSelf();
            var fsm = new InMemoryStateMachine();
            var follower = new Leader(currentState, sendToSelf, fsm);
            var log = new LogEntry("test", typeof(string), 1, 0);
            var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithEntry(log)
                .Build();
            //assume node has added the log..
            currentState.Log.Apply(log);
            var state = follower.Handle(appendEntries);
            state.State.ShouldBeOfType<Leader>();
            state.State.CurrentState.CurrentTerm.ShouldBe(1);
            state.State.CurrentState.LastApplied.ShouldBe(0);
            fsm.ExposedForTesting.ShouldBe(1);
        }
    }
}