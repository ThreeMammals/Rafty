using Xunit;
using Shouldly;
using Rafty.Concensus;
using System;
using System.Collections.Generic;
using Rafty.Log;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;
using Xunit.Abstractions;
using Rafty.FiniteStateMachine;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Rafty.AcceptanceTests
{


    public class Tests : IDisposable
    {
        private ConcurrentDictionary<int, Server> _servers;
        private List<IPeer> _peers;
        private int _numberOfServers;
        private readonly ITestOutputHelper _output;
        private KeyValuePair<int, Server> _previousLeader;

        public Tests(ITestOutputHelper output)
        {
            _output = output;
            _numberOfServers = 5;
            _servers = new ConcurrentDictionary<int, Server>();
            _peers = new List<IPeer>();
            for (int i = 0; i < _numberOfServers; i++)
            {
                var peer = new NodePeer();
                _peers.Add(peer);
            }
        }

        [Fact]
        public void ShouldElectLeader()
        {
            CreateServers();
            AssignNodesToPeers();
            StartNodes();
            AssertLeaderElected(4);
        }

        [Fact]
        public void ShouldElectAndRemainLeader()
        {
            CreateServers();
            AssignNodesToPeers();
            StartNodes();
            AssertLeaderElectedAndRemainsLeader();
        }

        [Fact]
        public void ShouldElectANewLeaderAfterPreviousOneDies()
        {
            CreateServers();
            AssignNodesToPeers();
            StartNodes();
            KillTheLeader();
            AssertLeaderElected(3);
        }

        [Fact]
        public void ShouldAllowPreviousLeaderBackIntoTheCluster()
        {
            CreateServers();
            AssignNodesToPeers();
            StartNodes();
            KillTheLeader();
            AssertLeaderElected(3);
            BringPreviousLeaderBackToLife();
            AssertLeaderElected(4);
        }

        [Fact]
        public void LeaderShouldAcceptCommandThenPersistToFollowersAndApplyToStateMachine()
        {
            CreateServers();
            AssignNodesToPeers();
            StartNodes();
            AssertLeaderElected(4);
            SendCommandToLeader();
            AssertCommandAccepted(1, 4);
        }

        [Fact]
        public void LeaderShouldAcceptManyCommandsThenPersistToFollowersAndApplyToStateMachine()
        {
            CreateServers();
            AssignNodesToPeers();
            StartNodes();
            AssertLeaderElected(4);
            SendCommandToLeader();
            AssertCommandAccepted(1, 4);
            SendCommandToLeader();
            AssertCommandAccepted(2, 4);
            SendCommandToLeader();
            AssertCommandAccepted(3, 4);
            SendCommandToLeader();
            AssertCommandAccepted(4, 4);
        }

        [Fact]
        public void ShouldCatchUpIfNodeDies()
        {
            CreateServers();
            AssignNodesToPeers();
            StartNodes();
            KillTheLeader();
            AssertLeaderElected(3);
            SendCommandToLeader();
            AssertCommandAccepted(1, 3);
            BringPreviousLeaderBackToLife();
            AssertLeaderElected(4);
            AssertCommandAccepted(1, 4);
            SendCommandToLeader();
            AssertCommandAccepted(2, 4);
        }

        private void SendCommandToLeader()
        {
            var leaderServer = _servers.First(x => x.Value.Node.State is Leader);
            var command = new FakeCommand();
            leaderServer.Value.Node.Accept(command);
        }

        private void AssertCommandAccepted(int expectedReplicatedCount, int expectedFollowers)
        {
            var leaderServer = _servers.First(x => x.Value.Node.State is Leader);
            var appliedToLeaderFsm = false;
            var stopWatch = Stopwatch.StartNew();
            while(stopWatch.Elapsed.Seconds < 25)
            {
                var finiteStateMachine = (InMemoryStateMachine)leaderServer.Value.Fsm;
                if(finiteStateMachine.ExposedForTesting == expectedReplicatedCount)
                {
                    appliedToLeaderFsm = true;
                    break;
                }
            }

            if(!appliedToLeaderFsm)
            {
                var leader = (Leader)leaderServer.Value.Node.State;
                _output.WriteLine($"Leader SendAppendEntriesCount {leader.SendAppendEntriesCount}");
                var inMemoryLog = (InMemoryLog)leaderServer.Value.Log;
                var inMemoryStateMachine = (InMemoryStateMachine)leaderServer.Value.Fsm;
                _output.WriteLine($"Leader log count {inMemoryLog.Count}");
                _output.WriteLine($"Leader fsm count {inMemoryStateMachine.ExposedForTesting}");
                throw new Exception("Command was not applied to leader state machine..");
            }

            appliedToLeaderFsm.ShouldBeTrue();
            var state = (Leader)leaderServer.Value.Node.State;
            var log = (InMemoryLog)leaderServer.Value.Log;
            var fsm = (InMemoryStateMachine)leaderServer.Value.Fsm;
            log.Count.ShouldBe(expectedReplicatedCount);
            fsm.ExposedForTesting.ShouldBe(expectedReplicatedCount);


            //check followers
            var followers = _servers.Select(x => x.Value).Where(x => x.Node.State.GetType() == typeof(Follower)).ToList();

            followers.Count.ShouldBe(expectedFollowers);

            foreach(var follower in followers)
            {
                var followerState = (Follower)follower.Node.State;
                var followerLog = (InMemoryLog)follower.Log;
                var followerFsm = (InMemoryStateMachine)follower.Fsm;
                followerLog.Count.ShouldBe(expectedReplicatedCount);
                followerFsm.ExposedForTesting.ShouldBe(expectedReplicatedCount);
            }
        }

        private void BringPreviousLeaderBackToLife()
        {
             //now we need to start that old node up..
            _previousLeader.Value.Node.Start();
            _servers.TryAdd(_previousLeader.Key, _previousLeader.Value);
        }

        private void KillTheLeader()
        {
            Thread.Sleep(2000);
            //so we know a leader was elected..
            //lets stop our current leader and see what happens..
            var leaderServer = _servers.First(x => x.Value.Node.State is Leader);
            leaderServer.Value.Node.Stop();
            // dont think need to do the below anymore as node stop calls into state stop..
            // var state = (Leader)leaderServer.Value.Node.State;
            // state.Stop();
            if (!_servers.TryRemove(leaderServer.Key, out Server removedLeader))
            {
                throw new Exception("Could not remove leader..");
            }
            _output.WriteLine($"Id - {leaderServer.Value.Node.State.CurrentState.Id}");
            _output.WriteLine($"Term - {leaderServer.Value.Node.State.CurrentState.CurrentTerm}");
            _output.WriteLine($"VotedFor - {leaderServer.Value.Node.State.CurrentState.VotedFor}");
            _output.WriteLine("leader dies...");
            _previousLeader = leaderServer;
        }

        private void ReportServers()
        {
            var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
            _output.WriteLine($"Leaders {leaders.Count}");
            foreach(var leader in leaders)
            {
                _output.WriteLine("Leader");
                _output.WriteLine($"Id {leader.State.CurrentState.Id}");
                _output.WriteLine($"Term {leader.State.CurrentState.CurrentTerm}");
                _output.WriteLine($"VotedFor {leader.State.CurrentState.VotedFor}");
            }

            var candidates = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
            _output.WriteLine($"Candidates {candidates.Count}");
            foreach(var candidate in candidates)
            {
                _output.WriteLine("Candidate");
                _output.WriteLine($"Id {candidate.State.CurrentState.Id}");
                _output.WriteLine($"Term {candidate.State.CurrentState.CurrentTerm}");
                _output.WriteLine($"VotedFor {candidate.State.CurrentState.VotedFor}");
            }

            var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
            _output.WriteLine($"Followers {followers.Count}");
            foreach(var follower in followers)
            {
                _output.WriteLine("Follower");
                _output.WriteLine($"Id {follower.State.CurrentState.Id}");
                _output.WriteLine($"Term {follower.State.CurrentState.CurrentTerm}");
                _output.WriteLine($"VotedFor {follower.State.CurrentState.VotedFor}");
            }
        }

        private void StartServer(int index)
        {
            var log = new InMemoryLog();
            var fsm = new InMemoryStateMachine();
            var random = new RandomDelay();
            var settings = new SettingsBuilder().WithMinTimeout(1000).WithMaxTimeout(3500).WithHeartbeatTimeout(50).Build();
            Func<CurrentState, List<IPeer>> getPeers = state => {
                var peersThatAreNotThisServer = _peers.Where(p => p?.Id != state.Id).ToList();
                return peersThatAreNotThisServer;
            };
            var node = new Node(fsm, log, getPeers, random, settings);
            var server = new Server(log, fsm, node);
            _servers.TryAdd(index, server);
        }

        
        private void AssertLeaderElected(int expectedFollowers)
        {
            var stopwatch = Stopwatch.StartNew();
            var passed = false;
            while(stopwatch.Elapsed.TotalSeconds < 25)
            {
                Thread.Sleep(1000);
                var leader = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                if (leader.Count > 0)
                {
                    if (leader.Count == 1 && followers.Count == expectedFollowers)
                    {
                        passed = true;
                        //leave loop leader elected..
                        break;
                    }
                }
            }

            if (!passed)
            {
                ReportServers();
                throw new Exception("A leader was not elected in 25 seconds");
            }

            ReportServers();
        }
        private void AssertLeaderElectedAndRemainsLeader()
        {
            var stopwatch = Stopwatch.StartNew();
            var passed = false;
            var leaderId = default(Guid);
            while(stopwatch.Elapsed.TotalSeconds < 25)
            {
                Thread.Sleep(1000);
                var leader = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                if (leader.Count > 0)
                {
                    if (leader.Count == 1 && followers.Count == 4)
                    {
                        //if the leader id hasnt been set set it...
                        if(leaderId == default(Guid))
                        {
                            leaderId = leader[0].State.CurrentState.Id;
                        }

                        //now check to see if we still have the same leader id...
                        if(leaderId != leader[0].State.CurrentState.Id)
                        {
                            throw new Exception("A new leader has been elected but this should not have happened...");
                        }
                        passed = true;
                    }
                }
            }

            if (!passed)
            {
                ReportServers();
                throw new Exception("A leader was not elected in 25 seconds");
            }

            ReportServers();
        }

        private void CreateServers()
        {
            for (int i = 0; i < _numberOfServers; i++)
            {   
                var localIndex = i;
                StartServer(localIndex);
            }
        }

        private void AssignNodesToPeers()
        {
            _output.WriteLine("set the node for each peer");
            for (int i = 0; i < _numberOfServers; i++)
            {
                var peer = (NodePeer)_peers[i];
                var server = _servers[i];
                peer.SetNode(server.Node);
            }
        }

        private void StartNodes()
        {
            _output.WriteLine("start the nodes");
            foreach(var server in _servers)
            {
                server.Value.Node.Start();
            }
        }
        public void Dispose()
        {
            foreach(var server in _servers.Select(x => x.Value))
            {
            }
        }
    }

    public class FakeCommand
    {
        public string Value => "FakeCommand";
    }
}