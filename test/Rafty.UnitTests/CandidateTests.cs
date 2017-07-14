using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rafty.Concensus;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    using Castle.Components.DictionaryAdapter;

    public class CandidateTests : IDisposable
    {
        private Node _node;
        private readonly Guid _id;
        private SendToSelf _sendToSelf;
        private CurrentState _currentState;

        public CandidateTests()
        {
            _id = Guid.NewGuid();
            _currentState = new CurrentState(_id, new List<IPeer>(), 0, default(Guid), TimeSpan.FromMilliseconds(0));
            _sendToSelf = new SendToSelf();
            _node = new Node(_currentState, _sendToSelf);
            _sendToSelf.SetNode(_node);
        }

        [Fact]
        public void ShouldIncrementCurrentTermWhenElectionStarts()
        {           
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.CurrentState.CurrentTerm.ShouldBe(1);
        }

        [Fact]
        public void ShouldVoteForSelfWhenElectionStarts()
        {           
            _node.State.ShouldBeOfType<Follower>();
            _node.Handle(new TimeoutBuilder().Build());
            _node.State.CurrentState.VotedFor.ShouldBe(_id);
        }

        [Fact]
        public async Task ShouldResetTimeoutWhenElectionStarts()
        {         
            var testingSendToSelf = new TestingSendToSelf(); 
            _node = new Node(_currentState, testingSendToSelf);
            testingSendToSelf.SetNode(_node);
            var candidate = new Candidate(_currentState, testingSendToSelf);
            candidate.Handle(new BeginElection());
            testingSendToSelf.Timeouts.Count.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldRequestVotesFromAllPeersWhenElectionStarts()
        {
            var peers = FakePeer.Build(4);
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0));
            var testingSendToSelf = new TestingSendToSelf();
            _node = new Node(_currentState, testingSendToSelf);
            testingSendToSelf.SetNode(_node);
            var candidate = new Candidate(_currentState, testingSendToSelf);
            candidate.Handle(new BeginElection());
            peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.RequestVoteResponses.Count.ShouldBe(1);
            });
        }

        public void Dispose()
        {
            _sendToSelf.Dispose();
        }
    }
}