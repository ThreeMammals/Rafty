namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Concensus;
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
        private readonly Guid _id;
        private CurrentState _currentState;

        public CandidateTests()
        {
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _id = Guid.NewGuid();
            _node = new NothingNode();
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
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
            var candidate = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, new SettingsBuilder().Build());
            candidate.BeginElection();
            candidate.CurrentState.VotedFor.ShouldBe(_id);
        }
    }
}