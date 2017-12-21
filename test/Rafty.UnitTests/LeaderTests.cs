using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rafty.Concensus;
using Rafty.Concensus.States;
using Rafty.FiniteStateMachine;
using Rafty.Log;
using Shouldly;
using Xunit;
using static Rafty.Infrastructure.Wait;

namespace Rafty.UnitTests
{
    public class LeaderTests
    {
        private readonly IFiniteStateMachine _fsm;
        private INode _node;
        private readonly string _id;
        private CurrentState _currentState;
        private List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _delay;
        private InMemorySettings _settings;
        private IRules _rules;

        public LeaderTests()
        {
            _rules = new Rules();
            _settings = new InMemorySettingsBuilder().Build();
            _delay = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _id = Guid.NewGuid().ToString();
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
            _node = new NothingNode();
        }

        [Fact()]
        public void ShouldSendEmptyAppendEntriesRpcOnElection()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules);
            bool TestPeers(List<IPeer> peers)
            {
                var passed = 0;

                peers.ForEach(x =>
                {
                    var peer = (FakePeer)x;
                    if (peer.AppendEntriesResponses.Count >= 1)
                    {
                        passed++;
                    }
                });

                return passed == peers.Count;
            }
            var result = WaitFor(1000).Until(() => TestPeers(_peers));
            result.ShouldBeTrue();
        }

        [Fact]
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
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, log, _node, _settings, _rules);
            leader.Accept(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact]
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
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, log, _node, _settings, _rules);
            var response = leader.Accept<FakeCommand>(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);

            var fsm = (InMemoryStateMachine)_fsm;
            fsm.ExposedForTesting.ShouldBe(1);
            response.ShouldBeOfType<OkResponse<FakeCommand>>();
        }

        [Fact]
        public void ShouldHandleCommandIfNoPeers()
        {
            _peers = new List<IPeer>();
            var log = new InMemoryLog();
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, log, _node, _settings, _rules);
            var response = leader.Accept<FakeCommand>(new FakeCommand());
            log.ExposedForTesting.Count.ShouldBe(1);
            var fsm = (InMemoryStateMachine)_fsm;
            fsm.ExposedForTesting.ShouldBe(1);
            response.ShouldBeOfType<OkResponse<FakeCommand>>();
        }

        [Fact]
        public void ShouldInitialiseNextIndex()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules);
    
            leader.PeerStates.ForEach(pS =>
            {
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(1);
            });
        }

        [Fact]
        public void ShouldInitialiseMatchIndex()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true));
            }
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState,_fsm, (s) => _peers, _log, _node, _settings, _rules);
            leader.PeerStates.ForEach(pS =>
            {
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(0);
            });
        }

        [Fact]
        public void ShouldInitialiseNextAndMatchIndexWhenNewPeerJoins()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 1; i++)
            {
                _peers.Add(new FakePeer(Guid.NewGuid().ToString()));
            }
            _currentState = new CurrentState(_id, 0, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState,_fsm, (s) => _peers, _log, _node, _settings, _rules);
            leader.PeerStates.Count.ShouldBe(1);
            leader.PeerStates.ForEach(pS =>
            {
                pS.NextIndex.NextLogIndexToSendToPeer.ShouldBe(1);
                pS.MatchIndex.IndexOfHighestKnownReplicatedLog.ShouldBe(0);
            });

            for (var i = 0; i < 3; i++)
            {
                _peers.Add(new FakePeer(Guid.NewGuid().ToString()));
            }
            
            bool TestPeerStates()
            {
                var passed = 0;

                leader.PeerStates.ForEach(pS =>
                {
                    if(leader.PeerStates.Count == 4 && pS.NextIndex.NextLogIndexToSendToPeer == 1 && pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 0)
                    {
                        passed++;
                    }
                });

                return passed == leader.PeerStates.Count;
            }

            var result = WaitFor(1000).Until(() => TestPeerStates());
            result.ShouldBeTrue();
        }
        
        [Fact]
        public void ShouldSendAppendEntriesStartingAtNextIndex()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true));
            }

            //add 3 logs
            var logOne = new LogEntry(new FakeCommand("1"), typeof(string), 1);
            _log.Apply(logOne);
            var logTwo = new LogEntry(new FakeCommand("2"), typeof(string), 1);
            _log.Apply(logTwo);
            var logThree = new LogEntry(new FakeCommand("3"), typeof(string), 1);
            _log.Apply(logThree);
            _currentState = new CurrentState(_id, 1, default(string), 2, 2, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules);
            var logs = _log.GetFrom(1);
            logs.Count.ShouldBe(3);
        }

        [Fact]
        public void ShouldUpdateMatchIndexAndNextIndexIfSuccessful()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true, true));
            }
            //add 3 logs
            _currentState = new CurrentState(_id, 1, default(string), 2, 2, default(string));
            var logOne = new LogEntry(new FakeCommand("1"), typeof(string), 1);
            _log.Apply(logOne);
            var logTwo = new LogEntry(new FakeCommand("2"), typeof(string), 1);
            _log.Apply(logTwo);
            var logThree = new LogEntry(new FakeCommand("3"), typeof(string), 1);
            _log.Apply(logThree);
            var leader = new Leader(_currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules);

            bool FirstTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 3)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 4)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            var result = WaitFor(1000).Until(() => FirstTest(leader.PeerStates));
            result.ShouldBeTrue();
        }

        [Fact]
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
            
            _currentState = new CurrentState(_id, 1, default(string), 1, 1, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules);

            //send first command, this wont get commited because the guys are replying false
            var task = Task.Run(async () => leader.Accept(new FakeCommand()));
            bool FirstTest(List<PeerState> peerState)
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
            result = WaitFor(1000).Until(() => FourthTest(leader.PeerStates));
            result.ShouldBeTrue();

            //send another command 
            leader.Accept(new FakeCommand());
            bool FirthTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 3)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 4)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            result = WaitFor(2000).Until(() => FirthTest(leader.PeerStates));
            result.ShouldBeTrue();
        }

        [Fact]
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
            _currentState = new CurrentState(_id, 1, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules);
            leader.Accept(new FakeCommand());
            leader.Accept(new FakeCommand());
            leader.Accept(new FakeCommand());

            bool PeersTest(List<PeerState> peerState)
            {
                var passed = 0;

                peerState.ForEach(pS =>
                {
                    if (pS.MatchIndex.IndexOfHighestKnownReplicatedLog == 3)
                    {
                        passed++;
                    }

                    if (pS.NextIndex.NextLogIndexToSendToPeer == 4)
                    {
                        passed++;
                    }
                });

                return passed == peerState.Count * 2;
            }
            var result = WaitFor(2000).Until(() => PeersTest(leader.PeerStates));
            leader.CurrentState.CommitIndex.ShouldBe(3);
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
            _currentState = new CurrentState(_id, 1, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState,_fsm, (s) => _peers, _log, _node, _settings, _rules);
            bool TestPeerStates(List<PeerState> peerState)
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
            var result = WaitFor(1000).Until(() => TestPeerStates(leader.PeerStates));
            result.ShouldBeTrue();
        }



        [Fact]
        public void ShouldReplicateCommand()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(true, true, true));
            }
            _currentState = new CurrentState(_id, 1, default(string), 0, 0, default(string));
            var leader = new Leader(_currentState,_fsm, (s) => _peers, _log, _node, _settings, _rules);
            var command = new FakeCommand();
            var response = leader.Accept(command);
            response.ShouldBeOfType<OkResponse<FakeCommand>>();
            bool TestPeerStates(List<PeerState> peerState)
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
            _currentState = new CurrentState(_id, 1, default(string),  0, 0, default(string));
            var leader = new Leader(_currentState, _fsm, (s) => _peers, _log, _node, _settings, _rules);
            bool TestPeerStates(List<PeerState> peerState)
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
            var result = WaitFor(1000).Until(() => TestPeerStates(leader.PeerStates));
            result.ShouldBeTrue();
        }

        [Fact]
        public void ShouldTimeoutAfterXSecondsIfCannotReplicateCommand()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 4; i++)
            {
                _peers.Add(new FakePeer(false, false, false));
            }
            _currentState = new CurrentState(_id, 1, default(string), 0, 0, default(string));
            _settings = new InMemorySettingsBuilder().WithCommandTimeout(1).Build();
            var leader = new Leader(_currentState,_fsm, (s) => _peers, _log, _node, _settings, _rules);
            var command = new FakeCommand();
            var response = leader.Accept(command);
            var error = (ErrorResponse<FakeCommand>)response;
            error.Error.ShouldBe("Unable to replicate command to peers due to timeout.");
            bool TestPeerStates(List<PeerState> peerState)
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
            var result = WaitFor(1000).Until(() => TestPeerStates(leader.PeerStates));
            _log.Count.ShouldBe(0);
            result.ShouldBeTrue();
        }

        [Fact]
        public void ShouldTimeoutAfterXSecondsIfCannotReplicateCommandAndRollbackIndexes()
        {
            _peers = new List<IPeer>();
            for (var i = 0; i < 3; i++)
            {
                _peers.Add(new FakePeer(false, false, false));
            }

            _peers.Add(new FakePeer(true, true, true));

            _currentState = new CurrentState(_id, 1, default(string), 0, 0, default(string));
            _settings = new InMemorySettingsBuilder().WithCommandTimeout(1).Build();
            var leader = new Leader(_currentState,_fsm, (s) => _peers, _log, _node, _settings, _rules);
            var command = new FakeCommand();
            var response = leader.Accept(command);
            var error = (ErrorResponse<FakeCommand>)response;
            error.Error.ShouldBe("Unable to replicate command to peers due to timeout.");
            bool TestPeerStates(List<PeerState> peerState)
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
            var result = WaitFor(1000).Until(() => TestPeerStates(leader.PeerStates));
            _log.Count.ShouldBe(0);
            result.ShouldBeTrue();
        }
    }
}