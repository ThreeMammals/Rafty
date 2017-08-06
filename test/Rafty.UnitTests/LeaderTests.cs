using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            bool TestPeers(List<IPeer> peers)
            {
                var passed = 0;

                peers.ForEach(x =>
                {
                    var peer = (FakePeer)x;
                    if (peer.AppendEntriesResponses.Count == 1)
                    {
                        passed++;
                    }
                });

                return passed == peers.Count;
            }
            var result = WaitFor(1000).Until(() => TestPeers(_peers));
            result.ShouldBeTrue();
        }

        [Fact(DisplayName = "If command received from client: append entry to local log")]
        public void ShouldAppendCommandToLocalLog()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                var peer = new RemoteControledPeer();
                peer.SetAppendEntriesResponse(new AppendEntriesResponse(1, true));
                _peers.Add(peer);
            }
            var log = new InMemoryLog();
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
            var leader = new Leader(_currentState, _fsm, _peers, log, _node, new SettingsBuilder().Build());
            leader.Accept(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact(DisplayName = "If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)")]
        public void ShouldApplyCommandToStateMachine()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                var peer = new RemoteControledPeer();
                peer.SetAppendEntriesResponse(new AppendEntriesResponse(1, true));
                _peers.Add(peer);
            }
            var log = new InMemoryLog();
            _currentState = new CurrentState(_id, 0, default(Guid), 0, 0);
            var leader = new Leader(_currentState, _fsm, _peers, log, _node, new SettingsBuilder().Build());
            var response = leader.Accept<FakeCommand>(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);

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
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(-1);
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
            _currentState = new CurrentState(_id, 1, default(Guid), 2, 2);
            var logOne = new LogEntry("1", typeof(string), 1, 0);
            _log.Apply(logOne);
            var logTwo = new LogEntry("2", typeof(string), 1, 1);
            _log.Apply(logTwo);
            var logThree = new LogEntry("3", typeof(string), 1, 2);
            _log.Apply(logThree);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());

            bool FirstTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 2)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 3)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            var result = WaitFor(1000).Until(() => FirstTest(leader.PeerStates));
            result.ShouldBeTrue();
        }

        [Fact(DisplayName = "If last log index ≥ nextIndex for a follower: send AppendEntries RPC with log entries starting at nextIndex If AppendEntries fails because of log inconsistency: decrement nextIndex and retry(§5.3)")]
        public void ShouldDecrementNextIndexAndRetry()
        {
            //create peers that will initially return false when asked to append entries...
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                var peer = new RemoteControledPeer();
                peer.SetAppendEntriesResponse(new AppendEntriesResponse(1, false));
                _peers.Add(peer);
            }
            
            _currentState = new CurrentState(_id, 1, default(Guid), 1, 1);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());

            //send first command, this wont get commited because the guys are replying false
            var task = Task.Run(async () => leader.Accept(new FakeCommand()));
            bool FirstTest(List<PeerState> peerState)
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
            var result = WaitFor(1000).Until(() => FirstTest(leader.PeerStates));
            result.ShouldBeTrue();
            //now the peers accept the append entries
            foreach (var peer in _peers)
            {
                var rcPeer = (RemoteControledPeer)peer;
                rcPeer.SetAppendEntriesResponse(new AppendEntriesResponse(1, true));
            }
            //wait on sending the command
            task.Wait();

            bool SecondTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 0)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 1)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            result = WaitFor(1000).Until(() => SecondTest(leader.PeerStates));
            result.ShouldBeTrue();

            //now the peers stop accepting append entries..
            foreach (var peer in _peers)
            {
                var rcPeer = (RemoteControledPeer)peer;
                rcPeer.SetAppendEntriesResponse(new AppendEntriesResponse(1, false));
            }

            //send another command, this wont get commited because the guys are replying false
            task = Task.Run(async () => leader.Accept(new FakeCommand()));
            bool ThirdTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 0)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 1)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            result = WaitFor(1000).Until(() => ThirdTest(leader.PeerStates));
            result.ShouldBeTrue();

            //now the peers accept the append entries
            foreach (var peer in _peers)
            {
                var rcPeer = (RemoteControledPeer)peer;
                rcPeer.SetAppendEntriesResponse(new AppendEntriesResponse(1, true));
            }
            task.Wait();

            bool FourthTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 1)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 2)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            result = WaitFor(1000).Until(() => FourthTest(leader.PeerStates));
            result.ShouldBeTrue();

            //send another command 
            leader.Accept(new FakeCommand());
            bool FirthTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 2)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 3)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            result = WaitFor(2000).Until(() => FirthTest(leader.PeerStates));
            result.ShouldBeTrue();
        }

        [Fact(DisplayName = "If there exists an N such that N > commitIndex, a majority of matchIndex[i] ≥ N, and log[N].term == currentTerm: set commitIndex = N(§5.3, §5.4)")]
        public void ShouldSetCommitIndex()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                var peer = new RemoteControledPeer();
                peer.SetAppendEntriesResponse(new AppendEntriesResponse(1, true));
                _peers.Add(peer);
            }
            //add 3 logs
            _currentState = new CurrentState(_id, 1, default(Guid), 1, 0);
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, new SettingsBuilder().Build());
            leader.Accept(new FakeCommand());
            leader.Accept(new FakeCommand());
            leader.Accept(new FakeCommand());

            leader.CurrentState.CommitIndex.ShouldBe(2);

            bool PeersTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 2)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 3)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            var result = WaitFor(2000).Until(() => PeersTest(leader.PeerStates));
            result.ShouldBeTrue();
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
            var result = WaitFor(1000).Until(() => TestPeerStates(leader.PeerStates));
            result.ShouldBeTrue();
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
            var result = WaitFor(1000).Until(() => TestPeerStates(leader.PeerStates));
            result.ShouldBeTrue();
        }
    }

    public class RemoteControledPeer : IPeer
    {
        private RequestVoteResponse _requestVoteResponse;
        private AppendEntriesResponse _appendEntriesResponse;
        public int RequestVoteResponses { get; private set; }
        public int AppendEntriesResponses { get; private set; }

        public RemoteControledPeer()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }

        public void SetRequestVoteResponse(RequestVoteResponse requestVoteResponse)
        {
            _requestVoteResponse = requestVoteResponse;
        }

        public void SetAppendEntriesResponse(AppendEntriesResponse appendEntriesResponse)
        {
            _appendEntriesResponse = appendEntriesResponse;
        }

        public RequestVoteResponse Request(RequestVote requestVote)
        {
            RequestVoteResponses++;
            return _requestVoteResponse;
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            AppendEntriesResponses++;
            return _appendEntriesResponse;
        }
    }
}