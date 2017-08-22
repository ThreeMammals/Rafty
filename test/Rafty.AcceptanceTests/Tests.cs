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

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Rafty.AcceptanceTests
{


    public class Tests : IDisposable
    {
        private ConcurrentDictionary<int, Server> _servers;
        private Thread[] _threads;
        private List<IPeer> _peers;
        private int _numberOfServers;
        private readonly ITestOutputHelper _output;


        public Tests(ITestOutputHelper output)
        {
            _output = output;
            _numberOfServers = 5;
            _servers = new ConcurrentDictionary<int, Server>();
            _threads = new Thread[_numberOfServers];
            _peers = new List<IPeer>();

            for (int i = 0; i < _numberOfServers; i++)
            {
                var peer = new NodePeer();
                _peers.Add(peer);
            }
        }

        [Fact]
        public void ShouldElectALeader()
        {
            //set up the servers on diff threads
            for (int i = 0; i < _numberOfServers; i++)
            {   
                var localIndex = i;
                var thread = new Thread(x => StartServer(localIndex));
                thread.Start();
                _threads[localIndex] = thread;
            }

            _output.WriteLine("wait for threads to start..");
            Thread.Sleep(2000);

            _output.WriteLine("set the node for each peer");
            for (int i = 0; i < _numberOfServers; i++)
            {
                var peer = (NodePeer)_peers[i];
                var server = _servers[i];
                peer.SetNode(server.Node);
            }
    
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
                    if (leader.Count == 1 && followers.Count == 4)
                    {
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

    //     [Fact]
    //     public void ShouldElectANewLeaderAfterPreviousOneDies()
    //     {
    //         //set up the servers on diff threads
    //         for (int i = 0; i < _numberOfServers; i++)
    //         {   
    //             var localIndex = i;
    //             var thread = new Thread(x => StartServer(localIndex));
    //             thread.Start();
    //             _threads[localIndex] = thread;
    //         }

    //         _output.WriteLine("wait for threads to start..");
    //         Thread.Sleep(2000);

    //         _output.WriteLine("set the node for each peer");
    //         for (int i = 0; i < _numberOfServers; i++)
    //         {
    //             var peer = (NodePeer)_peers[i];
    //             var server = _servers[i];
    //             peer.SetNode(server.Node);
    //         }

    //         _output.WriteLine("start each node");
    //         for (int i = 0; i < _numberOfServers; i++)
    //         {
    //             var server = _servers[i];
    //             var peers = _peers.Where(x => x?.Id != server?.Node?.Id).ToList();
    //             server.Node.Start(peers, TimeSpan.FromMilliseconds(1000));
    //         }

    //         var stopwatch = Stopwatch.StartNew();
    //         var passed = false;
    //         while (stopwatch.Elapsed.TotalSeconds < 25)
    //         {
    //             Thread.Sleep(1000);
    //             //assert
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             if (leaders.Count > 0)
    //             {
    //                 if (leaders.Count == 1 && followers.Count == 4)
    //                 {
    //                     passed = true;
    //                     break;
    //                 }
    //             }
    //         }

    //         if (!passed)
    //         {
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             _output.WriteLine($"Leaders {leaders.Count}");
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             _output.WriteLine($"Candidate {candidate.Count}");
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             _output.WriteLine($"Followers {followers.Count}");
    //             throw new Exception("A leader was not elected in 50 seconds");
    //         }

    //         _output.WriteLine("leader elected...");

    //         Thread.Sleep(2000);
    //         //so we know a leader was elected..
    //         //lets stop our current leader and see what happens..
    //         var leaderServer = _servers.First(x => x.Value.Node.State is Leader);
    //         leaderServer.Value.SendToSelf.Dispose();
    //         var state = (Leader)leaderServer.Value.Node.State;
    //         state.Stop();
    //         if (!_servers.TryRemove(leaderServer.Key, out Server test))
    //         {
    //             throw new Exception("Could not remove leader..");
    //         }
    //         _output.WriteLine($"Id - {leaderServer.Value.Node.State.CurrentState.Id}");
    //         _output.WriteLine($"Term - {leaderServer.Value.Node.State.CurrentState.CurrentTerm}");
    //         _output.WriteLine($"VotedFor - {leaderServer.Value.Node.State.CurrentState.VotedFor}");

    //         _output.WriteLine("leader dies...");

    //         //wait and see if we get a new leader..
    //         stopwatch = Stopwatch.StartNew();
    //         passed = false;
    //         while(stopwatch.Elapsed.TotalSeconds < 25)
    //         {
    //             Thread.Sleep(10000);
    //             //assert
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             if (leaders.Count > 0)
    //             {
    //                 if (leaders.Count == 1 && followers.Count == 3)
    //                 {
    //                     passed = true;
    //                     break;
    //                 }
    //             }
    //         }

    //         if(!passed)
    //         {
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             _output.WriteLine($"Leaders {leaders.Count}");
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             _output.WriteLine($"Candidate {candidate.Count}");
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             _output.WriteLine($"Followers {followers.Count}");
    //             foreach (var server in _servers)
    //             {
    //                 _output.WriteLine($"Id - {server.Value.Node.State.CurrentState.Id}");
    //                 _output.WriteLine($"Term - {server.Value.Node.State.CurrentState.CurrentTerm}");
    //                 _output.WriteLine($"VotedFor - {server.Value.Node.State.CurrentState.VotedFor}");
    //             }
    //             throw new Exception("Didnt elect new leader after 50 seconds");
    //         }

    //         _output.WriteLine("leader elected...");
    //     }


    //     [Fact]
    //     public void ShouldAllowOldLeaderBackIntoTheCluster()
    //     {
    //         //set up the servers on diff threads
    //         for (int i = 0; i < _numberOfServers; i++)
    //         {   
    //             var localIndex = i;
    //             var thread = new Thread(x => StartServer(localIndex));
    //             thread.Start();
    //             _threads[localIndex] = thread;
    //         }

    //         _output.WriteLine("wait for threads to start..");
    //         Thread.Sleep(2000);

    //         _output.WriteLine("set the node for each peer");
    //         for (int i = 0; i < _numberOfServers; i++)
    //         {
    //             var peer = (NodePeer)_peers[i];
    //             var server = _servers[i];
    //             peer.SetNode(server.Node);
    //         }

    //         _output.WriteLine("start each node");
    //         for (int i = 0; i < _numberOfServers; i++)
    //         {
    //             var server = _servers[i];
    //             var peers = _peers.Where(x => x?.Id != server?.Node?.Id).ToList();
    //             server.Node.Start(peers, TimeSpan.FromMilliseconds(1000));
    //         }       

    //         var stopwatch = Stopwatch.StartNew();
    //         var passed = false;
    //         while(stopwatch.Elapsed.TotalSeconds < 50)
    //         {
    //             Thread.Sleep(1000);
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             if (leaders.Count > 0)
    //             {
    //                 if (leaders.Count == 1 && followers.Count == 4)
    //                 {
    //                     passed = true;
    //                     break;
    //                 }
    //             }
    //         }

    //         if (!passed)
    //         {
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             _output.WriteLine($"Leaders {leaders.Count}");
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             _output.WriteLine($"Candidate {candidate.Count}");
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             _output.WriteLine($"Followers {followers.Count}");
    //             throw new Exception("A leader was not elected in 50 seconds");
    //         }

    //         //so we know a leader was elected..
    //         //lets stop our current leader and see what happens..
    //         var leaderServer = _servers.First(x => x.Value.Node.State.GetType() == typeof(Leader));
    //         leaderServer.Value.SendToSelf.Dispose();
    //         _servers.TryRemove(leaderServer.Key, out Server test);

    //         //wait and see if we get a new leader..
    //         stopwatch = Stopwatch.StartNew();
    //         passed = false;
    //         while(stopwatch.Elapsed.TotalSeconds < 50)
    //         {
    //             Thread.Sleep(1000);
    //             //assert
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             if (leaders.Count > 0)
    //             {
    //                 if (leaders.Count == 1 && followers.Count == 3)
    //                 {
    //                     passed = true;
    //                     break;
    //                 }
    //             }
    //         }

    //         if (!passed)
    //         {
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             _output.WriteLine($"Leaders {leaders.Count}");
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             _output.WriteLine($"Candidate {candidate.Count}");
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             _output.WriteLine($"Followers {followers.Count}");
    //             throw new Exception("Didnt elect new leader after 50 seconds");
    //         }

    //         //now we need to start that old node up..
    //         leaderServer.Value.SendToSelf.Restart();
    //         _servers.TryAdd(leaderServer.Key, leaderServer.Value);

    //         //wait and see if they go back into cluster ok
    //         stopwatch = Stopwatch.StartNew();
    //         passed = false;
    //         while(stopwatch.Elapsed.TotalSeconds < 50)
    //         {
    //             Thread.Sleep(10000);
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             if (leaders.Count > 0)
    //             {
    //                 if (leaders.Count == 1 && followers.Count == 4)
    //                 {
    //                     passed = true;
    //                     break;
    //                 }
    //             }
    //         }

    //         if(!passed)
    //         {
    //             var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
    //             _output.WriteLine($"Leaders {leaders.Count}");
    //             var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
    //             _output.WriteLine($"Candidate {candidate.Count}");
    //             var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
    //             _output.WriteLine($"Followers {followers.Count}");
    //             throw new Exception("Old leader did not join cluster in the correct way after 50 seconds");
    //         }
    //     }

        public void Dispose()
        {
            foreach(var server in _servers.Select(x => x.Value))
            {
            }
        }

        private void StartServer(int index)
        {
            var log = new InMemoryLog();
            var fsm = new InMemoryStateMachine();
            var random = new RandomDelay();
            var settings = new SettingsBuilder().WithMinTimeout(100).WithMaxTimeout(1000).WithHeartbeatTimeout(50).Build();
            var node = new Node(fsm, log, _peers, random, settings);
            var server = new Server(log, fsm, node);
            _servers.TryAdd(index, server);
        }
    }
}