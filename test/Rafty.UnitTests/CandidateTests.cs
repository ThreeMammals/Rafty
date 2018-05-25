namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Concensus;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Rafty.Concensus.States;
    using Rafty.FiniteStateMachine;
    using Rafty.Log;
    using Shouldly;
    using Xunit;

/*Candidates(�5.2):
� On conversion to candidate, start election:
� Increment currentTerm
� Vote for self
� Reset election timer
� Send RequestVote RPCs to all other servers
� If votes received from majority of servers: become leader
� If AppendEntries RPC received from new leader: convert to
follower
� If election timeout elapses: start new election*/

    public class CandidateTests
    {
        private IFiniteStateMachine _fsm;
        private ILog _log;
        private List<IPeer> _peers;
        private IRandomDelay _random;
        private INode _node;
        private readonly string _id;
        private CurrentState _currentState;
        private InMemorySettings _settings;
        private IRules _rules;
        private Mock<ILoggerFactory> _loggerFactory;
        private Mock<ILogger> _logger;

        public CandidateTests()
        {            
            _logger = new Mock<ILogger>();
            _loggerFactory = new Mock<ILoggerFactory>();
            _loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_logger.Object);
            _rules = new Rules();
            _settings = new InMemorySettingsBuilder().Build();
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _id = Guid.NewGuid().ToString();
            _node = new NothingNode();
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
        }

        [Fact]
        public void ShouldStartNewElectionIfTimesout()
        {
            _peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            candidate.ShouldBeOfType<Candidate>();
            candidate.CurrentState.CurrentTerm.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldBecomeFollowerIfAppendEntriesReceivedFromNewLeaderAndTermGreaterThanCurrentTerm()
        {
            _peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            var appendEntriesResponse = await candidate.Handle(new AppendEntriesBuilder().WithTerm(2).Build());
            appendEntriesResponse.Success.ShouldBeTrue();
            var node = (NothingNode)_node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        [Fact]
        public void ShouldBecomeFollowerIfRequestVoteResponseTermGreaterThanCurrentTerm()
        {
            _peers = new List<IPeer>
            {
                new FakePeer(10),
                new FakePeer(true),
                new FakePeer(true),
                new FakePeer(true)
            };
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            var node = (NothingNode)_node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldNotBecomeFollowerIfAppendEntriesReceivedFromNewLeaderAndTermLessThanCurrentTerm()
        {
            _peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            var appendEntriesResponse = await candidate.Handle(new AppendEntriesBuilder().WithTerm(0).Build());
            appendEntriesResponse.Success.ShouldBeFalse();
            var node = (NothingNode)_node;
            node.BecomeFollowerCount.ShouldBe(0);
        }

        [Fact]
        public void ShouldBecomeFollowerIfDoesntReceiveAnyVotes()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(false));
            }
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            var node = (NothingNode)_node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        [Fact]
        public void ShouldBecomeFollowerIfDoesntReceiveMajorityOfVotes()
        {
            _peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true)
            };
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            var node = (NothingNode)_node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        [Fact]
        public void ShouldBecomeLeaderIfReceivesMajorityOfVotes()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            var node = (NothingNode)_node;
            node.BecomeLeaderCount.ShouldBe(1);
        }

        [Fact]
        public void ShouldIncrementCurrentTermWhenElectionStarts()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            candidate.CurrentState.CurrentTerm.ShouldBe(1);
        }

        [Fact]
        public void ShouldRequestVotesFromAllPeersWhenElectionStarts()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            _peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.RequestVoteResponses.Count.ShouldBe(1);
            });
        }

        [Fact]
        public void ShouldResetTimeoutWhenElectionStarts()
        {
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
        }

        [Fact]
        public void ShouldVoteForSelfWhenElectionStarts()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            candidate.BeginElection();
            candidate.CurrentState.VotedFor.ShouldBe(_id);
        }

        [Fact]
        public async Task ShouldVoteForNewCandidateInAnotherTermsElection()
        {
            _node = new NothingNode();
            _currentState = new CurrentState(Guid.NewGuid().ToString(), 0, default(string), 0, 0, default(string));
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            var requestVote = new RequestVoteBuilder().WithTerm(0).WithCandidateId(Guid.NewGuid().ToString()).WithLastLogIndex(1).Build();
            var requestVoteResponse = await candidate.Handle(requestVote);
            candidate.CurrentState.VotedFor.ShouldBe(requestVote.CandidateId);
            requestVoteResponse.VoteGranted.ShouldBeTrue();
            requestVote = new RequestVoteBuilder().WithTerm(1).WithCandidateId(Guid.NewGuid().ToString()).WithLastLogIndex(1).Build();
            requestVoteResponse = await candidate.Handle(requestVote);
            requestVoteResponse.VoteGranted.ShouldBeTrue();
            candidate.CurrentState.VotedFor.ShouldBe(requestVote.CandidateId);
        }

        [Fact]
        public async Task CandidateShouldTellClientToRetryCommand()
        {
            _node = new NothingNode();
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            var response = await candidate.Accept(new FakeCommand());
            var error = (ErrorResponse<FakeCommand>)response;
            error.Error.ShouldBe("Please retry command later. Currently electing new a new leader.");
        }

        
        [Fact]
        public async Task CandidateShouldAppendNewEntries()
        {
            _currentState = new CurrentState(Guid.NewGuid().ToString(), 0, default(string), 0, 0, default(string));
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            var logEntry = new LogEntry(new FakeCommand(), typeof(FakeCommand), 1);
            var appendEntries = new AppendEntriesBuilder().WithTerm(1).WithEntry(logEntry).Build();
            var response = await candidate.Handle(appendEntries);
            response.Success.ShouldBeTrue();
            response.Term.ShouldBe(1);
            var inMemoryLog = (InMemoryLog)_log;
            inMemoryLog.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact]
        public async Task CandidateShouldNotAppendDuplicateEntry()
        {
            _node = new NothingNode();
            _currentState = new CurrentState(Guid.NewGuid().ToString(), 0, default(string), 0, 0, default(string));
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules, _loggerFactory.Object);
            var logEntry = new LogEntry(new FakeCommand(), typeof(FakeCommand), 1);
            var appendEntries = new AppendEntriesBuilder().WithPreviousLogIndex(1).WithPreviousLogTerm(1).WithTerm(1).WithEntry(logEntry).WithTerm(1).WithLeaderCommitIndex(1).Build();
            var response = await candidate.Handle(appendEntries);
            response.Success.ShouldBeTrue();
            response.Term.ShouldBe(1);
            response = await candidate.Handle(appendEntries);
            response.Success.ShouldBeTrue();
            response.Term.ShouldBe(1);
            var inMemoryLog = (InMemoryLog)_log;
            inMemoryLog.ExposedForTesting.Count.ShouldBe(1);
        }
    }
}