namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Concensus;
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
        public CandidateTests()
        {
            _id = Guid.NewGuid();
            _currentState = new CurrentState(_id, new List<IPeer>(), 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog());
            _sendToSelf = new TestingSendToSelf();
            _node = new Node(_currentState, _sendToSelf);
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
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog());
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf);
            var state = candidate.Handle(new TimeoutBuilder().Build());
            state.ShouldBeOfType<Candidate>();
            state.CurrentState.CurrentTerm.ShouldBe(2);
        }

        [Fact]
        public void ShouldBecomeFollowerIfAppendEntriesReceivedFromNewLeaderAndTermGreaterThanCurrentTerm()
        {
            var peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog());
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf);
            var state = candidate.Handle(new BeginElection());
            state = candidate.Handle(new AppendEntriesBuilder().WithTerm(2).Build());
            state.ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldNotBecomeFollowerIfAppendEntriesReceivedFromNewLeaderAndTermLessThanCurrentTerm()
        {
            var peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog());
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf);
            var state = candidate.Handle(new BeginElection());
            state = candidate.Handle(new AppendEntriesBuilder().WithTerm(0).Build());
            state.ShouldBeOfType<Candidate>();
        }

        [Fact]
        public void ShouldBecomeFollowerIfDoesntReceiveAnyVotes()
        {
            var peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                peers.Add(new FakePeer(false));
            }
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog());
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf);
            candidate.Handle(new BeginElection()).ShouldBeOfType<Follower>();
        }

        [Fact]
        public void ShouldBecomeFollowerIfDoesntReceiveMajorityOfVotes()
        {
            var peers = new List<IPeer>
            {
                new FakePeer(false),
                new FakePeer(false),
                new FakePeer(true),
                new FakePeer(true)
            };
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog());
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf);
            candidate.Handle(new BeginElection()).ShouldBeOfType<Leader>();
        }

        [Fact]
        public void ShouldBecomeLeaderIfReceivesMajorityOfVotes()
        {
            var peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog());
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf);
            candidate.Handle(new BeginElection()).ShouldBeOfType<Leader>();
        }

        [Fact]
        public void ShouldIncrementCurrentTermWhenElectionStarts()
        {
            var candidate = new Follower(_currentState, _sendToSelf);
            var state = candidate.Handle(new TimeoutBuilder().Build());
            state.ShouldBeOfType<Candidate>();
            state.CurrentState.CurrentTerm.ShouldBe(1);
        }

        [Fact]
        public void ShouldRequestVotesFromAllPeersWhenElectionStarts()
        {
            var peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog());
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf);
            candidate.Handle(new BeginElection());
            peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.RequestVoteResponses.Count.ShouldBe(1);
            });
        }

        [Fact]
        public void ShouldResetTimeoutWhenElectionStarts()
        {
            var testingSendToSelf = new TestingSendToSelf();
            var candidate = new Candidate(_currentState, testingSendToSelf);
            candidate.Handle(new BeginElection());
            testingSendToSelf.Timeouts.Count.ShouldBe(1);
        }

        [Fact]
        public void ShouldVoteForSelfWhenElectionStarts()
        {
            var candidate = new Candidate(_currentState, _sendToSelf);
            var state = candidate.Handle(new TimeoutBuilder().Build());
            state.CurrentState.VotedFor.ShouldBe(_id);
        }
    }
}