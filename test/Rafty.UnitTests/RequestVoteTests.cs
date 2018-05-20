using System;
using System.Collections.Generic;
using System.Text;
using Rafty.Concensus;
using Rafty.Concensus.States;
using Rafty.FiniteStateMachine;
using Rafty.Log;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;

    public class RequestVoteTests : IDisposable
    {
/*
1. Reply false if term<currentTerm (§5.1)
2. If votedFor is null or candidateId, and candidate’s log is at
least as up-to-date as receiver’s log, grant vote(§5.2, §5.4)
*/
        private readonly INode _node;
        private CurrentState _currentState;
        private IFiniteStateMachine _fsm;
        private List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _random;
        private InMemorySettings _settings;
        private IRules _rules;
        private Mock<ILoggerFactory> _loggerFactory;

        public RequestVoteTests()
        {
            _loggerFactory = new Mock<ILoggerFactory>();
            _rules = new Rules();
            _settings = new InMemorySettingsBuilder().Build();
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _node = new NothingNode();
        }

        public void Dispose()
        {
            //_node.Dispose();
        }

        [Fact]
        public async Task FollowerShouldReplyFalseIfTermIsLessThanCurrentTerm()
        {
            _currentState = new CurrentState(Guid.NewGuid().ToString(), 1, default(string), 1, 0, default(string));
            var requestVoteRpc = new RequestVoteBuilder().WithTerm(0).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings,_rules, _peers, _loggerFactory.Object);
            var requestVoteResponse = await follower.Handle(requestVoteRpc);
            requestVoteResponse.VoteGranted.ShouldBe(false);
            requestVoteResponse.Term.ShouldBe(1);
        }

        [Fact]
        public async Task FollowerShouldReplyFalseIfVotedForIsNotDefault()
        {
            _currentState = new CurrentState(Guid.NewGuid().ToString(), 1, Guid.NewGuid().ToString(), 1, 0, default(string));
            var requestVoteRpc = new RequestVoteBuilder().WithTerm(0).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings,_rules, _peers, _loggerFactory.Object);
            var requestVoteResponse = await follower.Handle(requestVoteRpc);
            requestVoteResponse.VoteGranted.ShouldBe(false);
            requestVoteResponse.Term.ShouldBe(1);
        }

        [Fact]
        public async Task FollowerShouldReplyFalseIfVotedForIsNotCandidateId()
        {
            _currentState = new CurrentState(Guid.NewGuid().ToString(), 1, Guid.NewGuid().ToString(), 1, 0, default(string));
            var requestVoteRpc = new RequestVoteBuilder().WithCandidateId(Guid.NewGuid().ToString()).WithTerm(0).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings,_rules, _peers, _loggerFactory.Object);
            var requestVoteResponse = await follower.Handle(requestVoteRpc);
            requestVoteResponse.VoteGranted.ShouldBe(false);
            requestVoteResponse.Term.ShouldBe(1);
        }

        [Fact]
        public async Task FollowerShouldGrantVote()
        {
            _currentState = new CurrentState(Guid.NewGuid().ToString(), 1, default(string), 1, 0, default(string));
            var requestVoteRpc = new RequestVoteBuilder().WithLastLogIndex(1).WithLastLogTerm(0).WithTerm(1).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings,_rules, _peers, _loggerFactory.Object);
            var requestVoteResponse = await follower.Handle(requestVoteRpc);
            requestVoteResponse.VoteGranted.ShouldBe(true);
            requestVoteResponse.Term.ShouldBe(1);
        }
    }
}
