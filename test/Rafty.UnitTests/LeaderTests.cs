using System;
using System.Collections.Generic;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;
using Rafty.Log;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    /*
    state
    nextIndex[] for each server, index of the next log entry
    to send to that server (initialized to leader
    last log index + 1)
    matchIndex[] for each server, index of highest log entry
    known to be replicated on server
    (initialized to 0, increases monotonically)

    behaviour
    • Upon election: send initial empty AppendEntries RPCs
    (heartbeat) to each server; repeat during idle periods to
    prevent election timeouts (§5.2)
    • If command received from client: append entry to local log,
    respond after entry applied to state machine (§5.3)
    • If last log index ≥ nextIndex for a follower: send
    AppendEntries RPC with log entries starting at nextIndex
    • If successful: update nextIndex and matchIndex for
    follower (§5.3)
    • If AppendEntries fails because of log inconsistency:
    decrement nextIndex and retry (§5.3)
    • If there exists an N such that N > commitIndex, a majority
    of matchIndex[i] ≥ N, and log[N].term == currentTerm:
    set commitIndex = N (§5.3, §5.4).
    */

    public class LeaderTests
    {
        private IFiniteStateMachine _fsm;
        private Node _node;
        private readonly Guid _id;
        private readonly ISendToSelf _sendToSelf;
        private CurrentState _currentState;
        
        public LeaderTests()
        {
            _fsm = new InMemoryStateMachine();
            _id = Guid.NewGuid();
            _currentState = new CurrentState(_id, new List<IPeer>(), 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog(), 0, 0);
            _sendToSelf = new TestingSendToSelf();
            _node = new Node(_currentState, _sendToSelf, _fsm);
            _sendToSelf.SetNode(_node);
        }

        public void Dispose()
        {
            _sendToSelf.Dispose();
        }

        [Fact(DisplayName = "Upon election: send initial empty AppendEntries RPCs(heartbeat) to each server")]
        public void ShouldSendEmptyAppendEntriesRpcOnElection()
        {
            var peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog(), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm);
            peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.AppendEntriesResponses.Count.ShouldBe(1);
            });
            testingSendToSelf.Timeouts.Count.ShouldBe(1);
        }

        [Fact(DisplayName = "Upon election: send initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts (§5.2)")]
        public void ShouldSendHeartbeats()
        {
            var peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), new InMemoryLog(), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm);
            leader.Handle(new TimeoutBuilder().Build());
            peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.AppendEntriesResponses.Count.ShouldBe(2);
            });
            testingSendToSelf.Timeouts.Count.ShouldBe(2);
        }

        [Fact(DisplayName = "If command received from client: append entry to local log")]
        public void ShouldAppendCommandToLocalLog()
        {
            var peers = new List<IPeer>();
            var log = new InMemoryLog();
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), log, 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm);
            leader.Handle<FakeCommand>(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact(DisplayName = "If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)")]
        public void ShouldApplyCommandToStateMachine()
        {
            var peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                peers.Add(new FakePeer(true, true));
            }
            var log = new InMemoryLog();
            _currentState = new CurrentState(_id, peers, 0, default(Guid), TimeSpan.FromMilliseconds(0), log, 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm);
            var response = leader.Handle<FakeCommand>(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);   
            peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.AppendEntriesResponses.Count.ShouldBe(2);
            });
            var fsm = (InMemoryStateMachine)_fsm;
            fsm.ExposedForTesting.ShouldBe(1);
            response.Success.ShouldBe(true);
        }
    }
}