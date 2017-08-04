namespace Rafty.AcceptanceTests
{
    using TestStack.BDDfy;
    using Shouldly;
    using Xunit;
    using Rafty.Concensus;
    using System;
    using System.Collections.Generic;
    using Rafty.Log;
    using System.Threading;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Diagnostics;
    using Xunit.Abstractions;

    public class Server
    {
        public Server(InMemoryLog log, SendToSelf sendToSelf, InMemoryStateMachine fsm, Node node)
        {
            this.Log = log;
            this.SendToSelf = sendToSelf;
            this.Fsm = fsm;
            this.Node = node;
        }
        public InMemoryLog Log { get; private set; }
        public SendToSelf SendToSelf { get; private set; }
        public InMemoryStateMachine Fsm { get; private set; }
        public Node Node { get; private set; }
    }

    public class NodePeer : IPeer
    {
        private Node _node;

        public Guid Id => _node.Id;

        public void SetNode (Node node)
        {
            _node = node;
        }

        public RequestVoteResponse Request(RequestVote requestVote)
        {
            return _node.Handle(requestVote);
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            return _node.Handle(appendEntries);
        }
    }

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

            _output.WriteLine("start each node");
            for (int i = 0; i < _numberOfServers; i++)
            {
                var server = _servers[i];
                var peers = _peers.Where(x => x?.Id != server?.Node?.Id).ToList();
                server.Node.Start(peers, TimeSpan.FromMilliseconds(5000));
            }       

            var stopwatch = Stopwatch.StartNew();
            var passed = false;
            while(stopwatch.Elapsed.TotalSeconds < 50)
            {
                _output.WriteLine("let the cpu do stuff");
                Thread.Sleep(1000);
                //assert
                var leader = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                _output.WriteLine($"Leaders {leader.Count}");
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                _output.WriteLine($"Candidate {candidate.Count}");
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                _output.WriteLine($"Followers {followers.Count}");
                if (leader.Count > 0)
                {
                    leader.Count.ShouldBe(1);
                    followers.Count.ShouldBe(4);
                    passed = true;
                    break;
                }
            }

            if (!passed)
            {
                throw new Exception("A leader was not elected in 50 seconds");
            }
        }

        [Fact]
        public void ShouldElectANewLeaderAfterPreviousOneDies()
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

            _output.WriteLine("start each node");
            for (int i = 0; i < _numberOfServers; i++)
            {
                var server = _servers[i];
                var peers = _peers.Where(x => x?.Id != server?.Node?.Id).ToList();
                server.Node.Start(peers, TimeSpan.FromMilliseconds(5000));
            }       

            var stopwatch = Stopwatch.StartNew();
            while(stopwatch.Elapsed.TotalSeconds < 50)
            {
                _output.WriteLine("let the cpu do stuff");
                Thread.Sleep(1000);
                //assert
                var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                _output.WriteLine($"Leaders {leaders.Count}");
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                _output.WriteLine($"Candidate {candidate.Count}");
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                _output.WriteLine($"Followers {followers.Count}");
                if (leaders.Count > 0)
                {
                    leaders.Count.ShouldBe(1);
                    followers.Count.ShouldBe(4);
                    break;
                }
            }

            //so we know a leader was elected..
            //lets stop our current leader and see what happens..
            var leaderServer = _servers.First(x => x.Value.Node.State.GetType() == typeof(Leader));
            leaderServer.Value.Node.Dispose();
            _servers.TryRemove(leaderServer.Key, out Server test);

            //wait and see if we get a new leader..
            stopwatch = Stopwatch.StartNew();
            var passed = false;
            while(stopwatch.Elapsed.TotalSeconds < 50)
            {
                _output.WriteLine("let the cpu do stuff");
                Thread.Sleep(1000);
                //assert
                var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                _output.WriteLine($"Leaders {leaders.Count}");
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                _output.WriteLine($"Candidate {candidate.Count}");
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                _output.WriteLine($"Followers {followers.Count}");
                if (leaders.Count > 0)
                {
                    leaders.Count.ShouldBe(1);
                    followers.Count.ShouldBe(3);
                    passed = true;
                    break;
                }
            }

            if(!passed)
            {
                throw new Exception("Didnt elect new leader after 50 seconds");
            }
            
        }


        [Fact(Skip = "brokes")]
        public void ShouldAllowOldLeaderBackIntoTheCluster()
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

            _output.WriteLine("start each node");
            for (int i = 0; i < _numberOfServers; i++)
            {
                var server = _servers[i];
                var peers = _peers.Where(x => x?.Id != server?.Node?.Id).ToList();
                server.Node.Start(peers, TimeSpan.FromMilliseconds(5000));
            }       

            var stopwatch = Stopwatch.StartNew();
            while(stopwatch.Elapsed.TotalSeconds < 50)
            {
                _output.WriteLine("let the cpu do stuff");
                Thread.Sleep(1000);
                //assert
                var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                _output.WriteLine($"Leaders {leaders.Count}");
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                _output.WriteLine($"Candidate {candidate.Count}");
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                _output.WriteLine($"Followers {followers.Count}");
                if (leaders.Count > 0)
                {
                    leaders.Count.ShouldBe(1);
                    followers.Count.ShouldBe(4);
                    break;
                }
            }

            //so we know a leader was elected..
            //lets stop our current leader and see what happens..
            var leaderServer = _servers.First(x => x.Value.Node.State.GetType() == typeof(Leader));
            leaderServer.Value.SendToSelf.Dispose();
            _servers.TryRemove(leaderServer.Key, out Server test);

            //wait and see if we get a new leader..
            stopwatch = Stopwatch.StartNew();
            while(stopwatch.Elapsed.TotalSeconds < 50)
            {
                _output.WriteLine("let the cpu do stuff");
                Thread.Sleep(1000);
                //assert
                var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                _output.WriteLine($"Leaders {leaders.Count}");
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                _output.WriteLine($"Candidate {candidate.Count}");
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                _output.WriteLine($"Followers {followers.Count}");
                if (leaders.Count > 0)
                {
                    leaders.Count.ShouldBe(1);
                    followers.Count.ShouldBe(3);
                    break;
                }
            }

            //now we need to start that old node up..
            leaderServer.Value.SendToSelf.Restart();
            _servers.TryAdd(leaderServer.Key, leaderServer.Value);

            //wait and see if they go back into cluster ok
            stopwatch = Stopwatch.StartNew();
            var passed = false;
            while(stopwatch.Elapsed.TotalSeconds < 50)
            {
                _output.WriteLine("let the cpu do stuff");
                Thread.Sleep(10000);
                //assert
                var leaders = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                _output.WriteLine($"Leaders {leaders.Count}");
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                _output.WriteLine($"Candidate {candidate.Count}");
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                _output.WriteLine($"Followers {followers.Count}");
                if (leaders.Count > 0)
                {
                    leaders.Count.ShouldBe(1);
                    followers.Count.ShouldBe(4);
                    passed = true;
                    break;
                }
            }

            if(!passed)
            {
                throw new Exception("Old leader did not join cluster in the correct way after 50 seconds");
            }
        }

        public void Dispose()
        {
            foreach(var server in _servers.Select(x => x.Value))
            {
                server.Node.Dispose();
            }
        }

        private void StartServer(int index)
        {
            var log = new InMemoryLog();
            var sendToSelf = new SendToSelf();
            var fsm = new InMemoryStateMachine();
            var random = new RandomDelay();
            var node = new Node(sendToSelf, fsm, log, random);
            sendToSelf.SetNode(node);
            var server = new Server(log, sendToSelf, fsm, node);
            _servers.TryAdd(index, server);
        }
    }
}