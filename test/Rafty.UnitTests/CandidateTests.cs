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

    public class CandidateTests : IDisposable
    {
        private IFiniteStateMachine _fsm;
        private ILog _log;
        private List<IPeer> _peers;

        public CandidateTests()
        {
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _id = Guid.NewGuid();
            _currentState = new CurrentState(_id, 0, default(Guid), 
                TimeSpan.FromMilliseconds(0), 0, 0);
            _sendToSelf = new TestingSendToSelf();
            _node = new Node(_sendToSelf, _fsm, _log);
            _sendToSelf.SetNode(_node);
        }

        public void Dispose()
        {
            _sendToSelf.Dispose();
        }

        private Node _node;
        private readonly Guid _id;
        private readonly ISendToSelf _sendToSelf;
        private CurrentState _currentState;

        [Fact]
        public void ShouldStartNewElectionIfTimesout()
        {
            var peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            _currentState = new CurrentState(_id, 0, default(Guid), 
                TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            var state = candidate.Handle(new TimeoutBuilder().Build());
            state.ShouldBeOfType<Candidate>();
            state.CurrentState.CurrentTerm.ShouldBe(2);
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
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            var state = candidate.Handle(new BeginElection());
            state = candidate.Handle(new AppendEntriesBuilder().WithTerm(2).Build());
            state.ShouldBeOfType<Follower>();
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
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            var state = candidate.Handle(new BeginElection());
            state.ShouldBeOfType<Follower>();
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
            _currentState = new CurrentState(_id, 0, default(Guid), 
            TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            var state = candidate.Handle(new BeginElection());
            state = candidate.Handle(new AppendEntriesBuilder().WithTerm(0).Build());
            state.ShouldBeOfType<Candidate>();
        }

        [Fact]
        public void ShouldBecomeFollowerIfDoesntReceiveAnyVotes()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(false));
            }
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            candidate.Handle(new BeginElection()).ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldBecomeFollowerIfDoesntReceiveMajorityOfVotes()
        {
            _peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            candidate.Handle(new BeginElection()).ShouldBeOfType<Leader>();
        }

        [Fact]
        public void ShouldBecomeLeaderIfReceivesMajorityOfVotes()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            candidate.Handle(new BeginElection()).ShouldBeOfType<Leader>();
        }

        [Fact]
        public void ShouldIncrementCurrentTermWhenElectionStarts()
        {
            var candidate = new Follower(_currentState, _sendToSelf, _fsm, _peers, _log);
            var state = candidate.Handle(new TimeoutBuilder().Build());
            state.ShouldBeOfType<Candidate>();
            state.CurrentState.CurrentTerm.ShouldBe(1);
        }

        [Fact]
        public void ShouldRequestVotesFromAllPeersWhenElectionStarts()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            candidate.Handle(new BeginElection());
            _peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.RequestVoteResponses.Count.ShouldBe(1);
            });
        }

        [Fact]
        public void ShouldResetTimeoutWhenElectionStarts()
        {
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf, _fsm, _peers, _log);
            var state = candidate.Handle(new BeginElection());
            //should be two because we set one when we begin the election and another because it results
            //in being a follower..
            testingSendToSelf.Timeouts.Count.ShouldBe(2);
            state.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldVoteForSelfWhenElectionStarts()
        {
            var candidate = new Candidate(_currentState, _sendToSelf, _fsm, _peers, _log);
            var state = candidate.Handle(new TimeoutBuilder().Build());
            state.CurrentState.VotedFor.ShouldBe(_id);
        }
    }
}