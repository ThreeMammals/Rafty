namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Concensus;
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
        public CandidateTests()
        {
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            candidate.BeginElection();
            candidate.ShouldBeOfType<Candidate>();
            candidate.CurrentState.CurrentTerm.ShouldBe(1);
        }

        [Fact]
        public void ShouldBecomeFollowerIfAppendEntriesReceivedFromNewLeaderAndTermGreaterThanCurrentTerm()
        {
            _peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            candidate.BeginElection();
            var appendEntriesResponse = candidate.Handle(new AppendEntriesBuilder().WithTerm(2).Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            candidate.BeginElection();
            var node = (NothingNode)_node;
            node.BecomeFollowerCount.ShouldBe(1);
        }

        [Fact]
        public void ShouldNotBecomeFollowerIfAppendEntriesReceivedFromNewLeaderAndTermLessThanCurrentTerm()
        {
            _peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            candidate.BeginElection();
            var appendEntriesResponse = candidate.Handle(new AppendEntriesBuilder().WithTerm(0).Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            candidate.BeginElection();
            candidate.CurrentState.VotedFor.ShouldBe(_id);
        }

        [Fact]
        public void ShouldVoteForNewCandidateInAnotherTermsElection()
        {
            _node = new NothingNode();
            _currentState = new CurrentState(Guid.NewGuid().ToString(), 0, default(string), 0, 0, default(string));
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            var requestVote = new RequestVoteBuilder().WithTerm(0).WithCandidateId(Guid.NewGuid().ToString()).WithLastLogIndex(1).Build();
            var requestVoteResponse = candidate.Handle(requestVote);
            candidate.CurrentState.VotedFor.ShouldBe(requestVote.CandidateId);
            requestVoteResponse.VoteGranted.ShouldBeTrue();
            requestVote = new RequestVoteBuilder().WithTerm(1).WithCandidateId(Guid.NewGuid().ToString()).WithLastLogIndex(1).Build();
            requestVoteResponse = candidate.Handle(requestVote);
            requestVoteResponse.VoteGranted.ShouldBeTrue();
            candidate.CurrentState.VotedFor.ShouldBe(requestVote.CandidateId);
        }

        [Fact]
        public void CandidateShouldTellClientToRetryCommand()
        {
            _node = new NothingNode();
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings, _rules);
            var response = candidate.Accept(new FakeCommand());
            var error = (ErrorResponse<FakeCommand>)response;
            error.Error.ShouldBe("Please retry command later. Currently electing new a new leader.");
        }
    }
}