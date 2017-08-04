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
        private List<IPeer> _peers;
        private ILog _log;
        private IRandomDelay _delay;
        
        public LeaderTests()
        {
            _delay = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _id = Guid.NewGuid();
            _currentState = new CurrentState(_id, 0, default(Guid), 
                TimeSpan.FromMilliseconds(0), 0, 0);
            _sendToSelf = new TestingSendToSelf();
            _node = new Node(_sendToSelf, _fsm, _log, _delay);
            _sendToSelf.SetNode(_node);
        }

        public void Dispose()
        {
            _sendToSelf.Dispose();
        }

        [Fact(DisplayName = "Upon election: send initial empty AppendEntries RPCs(heartbeat) to each server")]
        public void ShouldSendEmptyAppendEntriesRpcOnElection()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            _peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.AppendEntriesResponses.Count.ShouldBe(1);
            });
            testingSendToSelf.Timeouts.Count.ShouldBe(1);
        }

        [Fact(DisplayName = "Upon election: send initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts (§5.2)")]
        public void ShouldSendHeartbeats()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true));
            }
            _currentState = new CurrentState(_id, 2, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            var state = leader.Handle(new TimeoutBuilder().Build());
            _peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.AppendEntriesResponses.Count.ShouldBe(2);
            });
            testingSendToSelf.Timeouts.Count.ShouldBe(2);
            state.ShouldBeOfType<Leader>();
        }

        [Fact(DisplayName = "If command received from client: append entry to local log")]
        public void ShouldAppendCommandToLocalLog()
        {
            var log = new InMemoryLog();
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, log, _delay);
            leader.Accept<FakeCommand>(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact(DisplayName = "If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)")]
        public void ShouldApplyCommandToStateMachine()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true, true));
            }
            var log = new InMemoryLog();
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            var response = leader.Accept<FakeCommand>(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);   
            _peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.AppendEntriesResponses.Count.ShouldBe(2);
            });
            var fsm = (InMemoryStateMachine)_fsm;
            fsm.ExposedForTesting.ShouldBe(1);
            response.Success.ShouldBe(true);
        }

        [Fact(DisplayName = "for each server, index of the next log entry to send to that server(initialized to leader last log index + 1)")]
        public void ShouldInitialiseNextIndex()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            var expected = _log.LastLogIndex;
            leader.PeerStates.ForEach(pS =>
            {
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(expected);
            });
        }

        [Fact(DisplayName = "for each server, index of highest log entry known to be replicated on server (initialized to 0, increases monotonically)")]
        public void ShouldInitialiseMatchIndex()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(0);
            });
        }
        
        [Fact(DisplayName = "If last log index ≥ nextIndex for a follower: send AppendEntries RPC with log entries starting at nextIndex")]
        public void ShouldSendAppendEntriesStartingAtNextIndex()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true));
            }

            //add 3 logs
            var logOne = new LogEntry("1", typeof(string), 1, 0);
            _log.Apply(logOne);
            var logTwo = new LogEntry("2", typeof(string), 1, 1);
            _log.Apply(logTwo);
            var logThree = new LogEntry("3", typeof(string), 1, 2);
            _log.Apply(logThree);
            _currentState = new CurrentState(_id, 1, default(Guid), TimeSpan.FromMilliseconds(0), 2, 2);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            var logs = leader.GetLogsForPeer(new NextIndex(new FakePeer(true, true), 0));
            logs.Count.ShouldBe(3);
        }

        [Fact(DisplayName = "If last log index ≥ nextIndex for a follower: send AppendEntries RPC with log entries starting at nextIndex If successful: update nextIndex and matchIndex for follower(§5.3)")]
        public void ShouldUpdateMatchIndexAndNextIndexIfSuccessful()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true, true));
            }
            //add 3 logs
            _currentState = new CurrentState(_id, 1, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            var logOne = new LogEntry("1", typeof(string), 1, 0);
            _log.Apply(logOne);
            var logTwo = new LogEntry("2", typeof(string), 1, 1);
            _log.Apply(logTwo);
            var logThree = new LogEntry("3", typeof(string), 1, 2);
            _log.Apply(logThree);
            leader.Handle(new TimeoutBuilder().Build());
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(2);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(3);
            });
        }

        [Fact(DisplayName = "If last log index ≥ nextIndex for a follower: send AppendEntries RPC with log entries starting at nextIndex If AppendEntries fails because of log inconsistency: decrement nextIndex and retry(§5.3)")]
        public void ShouldDecrementNextIndexAndRetry()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, false, false, true));
            }
            //add 3 logs
            var logOne = new LogEntry("1", typeof(string), 1, 0);
            var logTwo = new LogEntry("2", typeof(string), 1, 1);
            _log.Apply(logTwo);
            _log.Apply(logOne);
            _currentState = new CurrentState(_id, 1, default(Guid), TimeSpan.FromMilliseconds(0), 1, 1);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            //initial we know that we replicated log 0?
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(0);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(1);
            });
            var logThree = new LogEntry("3", typeof(string), 1, 2);
            _log.Apply(logThree);
            //all servers fail to accept append entries
            leader.Handle(new TimeoutBuilder().Build());
            //something went wrong so we decrement next log index
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(0);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(0);
            });
            //retry and things back to normal..
            leader.Handle(new TimeoutBuilder().Build());
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(2);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(3);
            });
        }

        [Fact(DisplayName = "If there exists an N such that N > commitIndex, a majority of matchIndex[i] ≥ N, and log[N].term == currentTerm: set commitIndex = N(§5.3, §5.4)")]
        public void ShouldSetCommitIndex()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true, true, true));
            }
            //add 3 logs
            _currentState = new CurrentState(_id, 1, default(Guid), TimeSpan.FromMilliseconds(0), 1, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            var logOne = new LogEntry("1", typeof(string), 1, 0);
            _log.Apply(logOne);
            var logTwo = new LogEntry("2", typeof(string), 1, 1);
            _log.Apply(logTwo);
            var logThree = new LogEntry("3", typeof(string), 1, 2);
            _log.Apply(logThree);
            leader.Handle(new TimeoutBuilder().Build());
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(2);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(3);
            });
            leader.CurrentState.CommitIndex.ShouldBe(2);
        }

        [Fact]
        public void ShouldBeAbleToHandleWhenLeaderHasNoLogsAndCandidatesReturnSuccess()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true, true));
            }
            _currentState = new CurrentState(_id, 1, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            leader.Handle(new TimeoutBuilder().Build());
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(-1);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(0);
            });
        }

        [Fact]
        public void ShouldBeAbleToHandleWhenLeaderHasNoLogsAndCandidatesReturnFail()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true, true));
            }
            _currentState = new CurrentState(_id, 1, default(Guid), TimeSpan.FromMilliseconds(0), 0, 0);
            var testingSendToSelf = new TestingSendToSelf();
            var leader = new Leader(_currentState, testingSendToSelf, _fsm, _peers, _log, _delay);
            leader.Handle(new TimeoutBuilder().Build());
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(-1);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(0);
            });
        }
    }
}