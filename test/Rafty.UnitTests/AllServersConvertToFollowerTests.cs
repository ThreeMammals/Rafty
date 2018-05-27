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
    using System.Threading.Tasks;
    using Concensus.Messages;
    using Concensus.Node;
    using Concensus.Peers;
    using Rafty.Log;
    using Rafty.FiniteStateMachine;
    using Rafty.Concensus.States;

    /*
        If commitIndex > lastApplied: increment lastApplied, apply
        log[lastApplied] to state machine (§5.3)

        If RPC request or response contains term T > currentTerm:
        set currentTerm = T, convert to follower (§5.1)
    */

    public class AllServersConvertToFollowerTests
    {
        private readonly IFiniteStateMachine _fsm;
        private readonly ILog _log;
        private readonly List<IPeer> _peers;
        private readonly IRandomDelay _random;
        private readonly INode _node;
        private readonly InMemorySettings _settings;
        private readonly IRules _rules;
        private readonly Mock<ILoggerFactory> _loggerFactory;

        public AllServersConvertToFollowerTests()
        {
            _loggerFactory = new Mock<ILoggerFactory>();
            var logger = new Mock<ILogger>();
            _loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
            _rules = new Rules(_loggerFactory.Object, new NodeId(default(string)));
            _settings = new InMemorySettingsBuilder().Build();
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _node = new NothingNode();
        }

        [Theory]
        [InlineData(0, 1, 1, true)]
        [InlineData(2, 1, 2, false)]
        public async Task FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm, bool expectedResponse)
        {
            var currentState = new CurrentState(Guid.NewGuid().ToString(),currentTerm, default(string), 0, 0, default(string));
            var follower = new Follower(currentState, _fsm, _log, _random, _node, _settings, _rules, _peers, _loggerFactory.Object);
            var response = await follower.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            response.Success.ShouldBe(expectedResponse);
            follower.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 1, 1, true)]
        [InlineData(2, 1, 2, false)]
        public async Task FollowerShouldSetTermAsRpcTermAndStayFollowerWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm, bool expectedResponse)
        {
            var currentState = new CurrentState(Guid.NewGuid().ToString(), currentTerm, default(string), 0, 0, default(string));
            var follower = new Follower(currentState, _fsm, _log, _random, _node, _settings, _rules, _peers, _loggerFactory.Object);
            var response = await follower.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).Build());
            response.VoteGranted.ShouldBe(expectedResponse);
            follower.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, true)]
        [InlineData(2, 3, 3, true)]
        public async Task CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm, bool expectedResponse)
        {
            var currentState = new CurrentState(Guid.NewGuid().ToString(), currentTerm, default(string), 0, 0, default(string));
            var candidate = new Candidate(currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            var response = await candidate.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            response.Success.ShouldBe(expectedResponse);
            candidate.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
            var node = (NothingNode)_node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        [Theory]
        [InlineData(0, 2, 2, true)]
        [InlineData(2, 3, 3, true)]
        public async Task CandidateShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm, bool expectedResponse)
        {
            var currentState = new CurrentState(Guid.NewGuid().ToString(), currentTerm, default(string), 0, 0, default(string));
            var candidate = new Candidate(currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            var response = await candidate.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).WithLastLogIndex(1).Build());
            response.VoteGranted.ShouldBe(expectedResponse);
            candidate.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
            var node = (NothingNode) _node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        [Theory]
        [InlineData(0, 2, 2, true)]
        [InlineData(2, 3, 3, true)]
        public async Task LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesAppendEntries(int currentTerm, int rpcTerm, int expectedTerm, bool expectedResponse)
        {            
            var currentState = new CurrentState(Guid.NewGuid().ToString(), currentTerm, default(string), 0, 0, default(string));
            var leader = new Leader(currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules, _loggerFactory.Object);
            var response = await leader.Handle(new AppendEntriesBuilder().WithTerm(rpcTerm).Build());
            response.Success.ShouldBe(expectedResponse);
            leader.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }

        [Theory]
        [InlineData(0, 2, 2, true)]
        [InlineData(2, 3, 3, true)]
        public async Task LeaderShouldSetTermAsRpcTermAndBecomeStateWhenReceivesRequestVote(int currentTerm, int rpcTerm, int expectedTerm, bool expectedResponse)
        {
            var currentState = new CurrentState(Guid.NewGuid().ToString(), currentTerm, default(string), 0, 0, default(string));
            var leader = new Leader(currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules, _loggerFactory.Object);
            var response = await leader.Handle(new RequestVoteBuilder().WithTerm(rpcTerm).WithLastLogIndex(1).Build());
            response.VoteGranted.ShouldBe(expectedResponse);
            leader.CurrentState.CurrentTerm.ShouldBe(expectedTerm);
        }
    }
}