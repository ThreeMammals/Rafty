using System;
using System.Collections.Generic;
using System.Threading;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;
using Rafty.Log;
using Shouldly;
using Xunit;
using static Rafty.UnitTests.Wait;

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
        private readonly IFiniteStateMachine _fsm;
        private INode _node;
        private readonly Guid _id;
        private CurrentState _currentState;
        private List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _delay;
        
        public LeaderTests()
        {
            _delay = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _id = Guid.NewGuid();
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
            _node = new NothingNode();
        }

        [Fact(DisplayName = "Upon election: send initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts (§5.2)")]
        public void ShouldSendEmptyAppendEntriesRpcOnElection()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());
            _peers.ForEach(x =>
            {
                var peer = (FakePeer) x;
                peer.AppendEntriesResponses.Count.ShouldBe(1);
            });
        }

        [Fact(DisplayName = "If command received from client: append entry to local log")]
        public void ShouldAppendCommandToLocalLog()
        {
            var log = new InMemoryLog();
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
            var leader = new Leader(_currentState, _fsm, _peers, log, _node, new SettingsBuilder().Build());
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
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
            var leader = new Leader(_currentState, _fsm, _peers, log, _node, new SettingsBuilder().Build());
            var response = leader.Accept<FakeCommand>(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);
            bool TestPeers(List<IPeer> peers)
            {
                var passed = 0;

                peers.ForEach(x =>
                {
                    var peer = (FakePeer) x;
                    if (peer.AppendEntriesResponses.Count == 2)
                    {
                        passed++;
                    }
                });

                return passed == peers.Count;
            }
            WaitFor(1000).Until(() => TestPeers(_peers));
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
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());
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
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
            var leader = new Leader(_currentState,_fsm, _peers, _log, _node, new SettingsBuilder().Build());
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
            _currentState = new CurrentState(_id, 1, default(Guid), 2, 2);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());
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
            _currentState = new CurrentState(_id, 1, default(Guid), 0, 0);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());
            var logOne = new LogEntry("1", typeof(string), 1, 0);
            _log.Apply(logOne);
            var logTwo = new LogEntry("2", typeof(string), 1, 1);
            _log.Apply(logTwo);
            var logThree = new LogEntry("3", typeof(string), 1, 2);
            _log.Apply(logThree);
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
            
            _currentState = new CurrentState(_id, 1, default(Guid), 1, 1);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());
            leader.Accept(new FakeCommand());
            leader.Accept(new FakeCommand());
            //initial we know that we replicated log 0?
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(0);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(1);
            });

            leader.Accept(new FakeCommand());
            //all servers fail to accept append entries
            //something went wrong so we decrement next log index
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(0);
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(0);
            });
            //retry and things back to normal..
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
            _currentState = new CurrentState(_id, 1, default(Guid), 1, 0);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());
            leader.Accept(new FakeCommand());
            leader.Accept(new FakeCommand());
            leader.Accept(new FakeCommand());
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
            _currentState = new CurrentState(_id, 1, default(Guid), 0, 0);
            var leader = new Leader(_currentState,_fsm, _peers, _log, _node, new SettingsBuilder().Build());
            bool TestPeerStates(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == -1)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 0)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            WaitFor(1000).Until(() => TestPeerStates(leader.PeerStates));
        }

        [Fact]
        public void ShouldBeAbleToHandleWhenLeaderHasNoLogsAndCandidatesReturnFail()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true, true));
            }
            _currentState = new CurrentState(_id, 1, default(Guid),  0, 0);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());
            bool TestPeerStates(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == -1)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 0)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            WaitFor(1000).Until(() => TestPeerStates(leader.PeerStates));
        }
    }
}