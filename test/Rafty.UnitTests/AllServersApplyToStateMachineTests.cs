using Xunit;
using TestStack.BDDfy;
using Shouldly;
using Rafty.Concensus;
using System;
using System.Collections.Generic;
using Rafty.FiniteStateMachine;
using Rafty.Log;

namespace Rafty.UnitTests
{
    public class AllServersApplyToStateMachineTests
    {
/*
• If commitIndex > lastApplied: increment lastApplied, apply log[lastApplied] to state machine (§5.3)\
*/
        private List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _random;
        private readonly INode _node;
        private IFiniteStateMachine _fsm;

        public AllServersApplyToStateMachineTests()
        {
            _random = new RandomDelay();
            _peers = new List<IPeer>();
            _log = new InMemoryLog();
            _fsm = new InMemoryStateMachine();
            _node = new NothingNode();
        }

        [Fact] 
        public void FollowerShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid(), 0, default(Guid), -1, -1);
            var fsm = new InMemoryStateMachine();
            var follower = new Follower(currentState, fsm, _log, _random, _node, new SettingsBuilder().Build());
            var log = new LogEntry("test", typeof(string), 1);
            var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithPreviousLogTerm(1)
                .WithEntry(log)
                .Build();
            //assume node has added the log..
            _log.Apply(log);
            var appendEntriesResponse = follower.Handle(appendEntries);
            follower.CurrentState.CurrentTerm.ShouldBe(1);
            follower.CurrentState.LastApplied.ShouldBe(0);
            fsm.ExposedForTesting.ShouldBe(1);
        }

         [Fact] 
        public void CandidateShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid(), 0, default(Guid), -1, -1);
            var fsm = new InMemoryStateMachine();
            var candidate = new Candidate(currentState,fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
            var log = new LogEntry("test", typeof(string), 1);
            var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithPreviousLogTerm(1)
                .WithEntry(log)
                .Build();
            //assume node has added the log..
            _log.Apply(log);
            var appendEntriesResponse = candidate.Handle(appendEntries);
            candidate.CurrentState.CurrentTerm.ShouldBe(1);
            candidate.CurrentState.LastApplied.ShouldBe(0);
            fsm.ExposedForTesting.ShouldBe(1);
            var node = (NothingNode) _node;
            node.BecomeFollowerCount.ShouldBe(1);
        }


    /*     [Fact] 
        public void LeaderShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), TimeSpan.FromSeconds(0), -1, -1);
            var sendToSelf = new TestingSendToSelf();
            var fsm = new InMemoryStateMachine();
            var follower = new Leader(currentState, sendToSelf, fsm, _peers, _log, _random);
            var log = new LogEntry("test", typeof(string), 1, 0);
            var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithEntry(log)
                .Build();
            //assume node has added the log..
            _log.Apply(log);
            var state = follower.Handle(appendEntries);
            state.ShouldBeOfType<Leader>();
            state.CurrentState.CurrentTerm.ShouldBe(1);
            state.CurrentState.LastApplied.ShouldBe(0);
            fsm.ExposedForTesting.ShouldBe(1);
        }*/
    }
}