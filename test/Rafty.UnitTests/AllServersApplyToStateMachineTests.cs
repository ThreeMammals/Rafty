namespace Rafty.UnitTests
{
    using Infrastructure;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;
    using Shouldly;
    using Rafty.Concensus;
    using System;
    using System.Collections.Generic;
    using Rafty.FiniteStateMachine;
    using Rafty.Log;
    using Rafty.Concensus.States;
    using System.Threading.Tasks;
    using Concensus.Messages;
    using Concensus.Node;
    using Concensus.Peers;

    /*
    If commitIndex > lastApplied: increment lastApplied, apply log[lastApplied] to state machine (§5.3)\
    */
    public class AllServersApplyToStateMachineTests
    {
        private readonly List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _random;
        private readonly INode _node;
        private IFiniteStateMachine _fsm;
        private readonly InMemorySettings _settings;
        private readonly IRules _rules;
        private readonly Mock<ILoggerFactory> _loggerFactory;

        public AllServersApplyToStateMachineTests()
        {
            _loggerFactory = new Mock<ILoggerFactory>();
            var logger = new Mock<ILogger>();
            _loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
            _rules = new Rules(_loggerFactory.Object, new NodeId(""));
            _settings = new InMemorySettingsBuilder().Build();
            _random = new RandomDelay();
            _peers = new List<IPeer>();
            _log = new InMemoryLog();
            _fsm = new InMemoryStateMachine();
            _node = new NothingNode();
        }

        [Fact] 
        public async Task FollowerShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid().ToString(), 0, default(string), 0, 0, default(string));
            var fsm = new InMemoryStateMachine();
            var follower = new Follower(currentState, fsm, _log, _random, _node, _settings, _rules, _peers, _loggerFactory.Object);
            var log = new LogEntry(new FakeCommand("test"), typeof(FakeCommand), 1);
            var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithPreviousLogTerm(1)
                .WithLeaderCommitIndex(1)
                .WithPreviousLogIndex(1)
                .WithEntry(log)
                .Build();
            await _log.Apply(log);
            var appendEntriesResponse = await follower.Handle(appendEntries);
            appendEntriesResponse.Success.ShouldBeTrue();
            follower.CurrentState.CurrentTerm.ShouldBe(1);
            follower.CurrentState.LastApplied.ShouldBe(1);
            fsm.HandledLogEntries.ShouldBe(1);
        }

        [Fact] 
        public async Task CandidateShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid().ToString(), 0, default(string), 0, 0, default(string));
            var fsm = new InMemoryStateMachine();
            var candidate = new Candidate(currentState,fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            var log = new LogEntry(new FakeCommand("test"), typeof(string), 1);
            var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithPreviousLogTerm(1)
                .WithEntry(log)
                .WithPreviousLogIndex(1)
                .WithLeaderCommitIndex(1)
                .Build();
            await _log.Apply(log);
            var appendEntriesResponse = await candidate.Handle(appendEntries);
            appendEntriesResponse.Success.ShouldBeTrue();
            candidate.CurrentState.CurrentTerm.ShouldBe(1);
            candidate.CurrentState.LastApplied.ShouldBe(1);
            fsm.HandledLogEntries.ShouldBe(1);
            var node = (NothingNode) _node;
            node.BecomeFollowerCount.ShouldBe(1);
        }


        [Fact] 
        public async Task LeaderShouldApplyLogsToFsm()
        {
            var currentState = new CurrentState(Guid.NewGuid().ToString(), 0, default(string), 0, 0, default(string));
            var fsm = new InMemoryStateMachine();
            var leader = new Leader(currentState, fsm, s => _peers, _log, _node, _settings, _rules, _loggerFactory.Object);
            var log = new LogEntry(new FakeCommand("test"), typeof(string), 1);
               var appendEntries = new AppendEntriesBuilder()
                .WithTerm(1)
                .WithPreviousLogTerm(1)
                .WithEntry(log)
                .WithPreviousLogIndex(1)
                .WithLeaderCommitIndex(1)
                .Build();
            await _log.Apply(log);
            var appendEntriesResponse = await leader.Handle(appendEntries);
            appendEntriesResponse.Success.ShouldBeTrue();
            leader.CurrentState.CurrentTerm.ShouldBe(1);
            leader.CurrentState.LastApplied.ShouldBe(1);
            fsm.HandledLogEntries.ShouldBe(1);
        }
    }
}