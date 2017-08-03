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

        public Tests()
        {
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

            //wait for threads to start..
            Thread.Sleep(2000);

            //set the node for each peer
            for (int i = 0; i < _numberOfServers; i++)
            {
                var peer = (NodePeer)_peers[i];
                var server = _servers[i];
                peer.SetNode(server.Node);
            }     

            //start each node
            for (int i = 0; i < _numberOfServers; i++)
            {
                var server = _servers[i];
                var peers = _peers.Where(x => x?.Id != server?.Node?.Id).ToList();
                server.Node.Start(peers, TimeSpan.FromMilliseconds(5000));
            }       

            var stopwatch = Stopwatch.StartNew();

            while(stopwatch.Elapsed.TotalSeconds < 50)
            {
                //let the cpu do stuff
                Thread.Sleep(1000);
                //assert
                var leader = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Leader)).ToList();
                var candidate = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Candidate)).ToList();
                var followers = _servers.Select(x => x.Value.Node).Where(x => x.State.GetType() == typeof(Follower)).ToList();
                if(leader.Count > 0)
                {
                    leader.Count.ShouldBe(1);
                    followers.Count.ShouldBe(4);
                }
            }

            throw new Exception("A leader was not elected in 50 seconds");
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
            var node = new Node(sendToSelf, fsm, log);
            sendToSelf.SetNode(node);
            var server = new Server(log, sendToSelf, fsm, node);
            _servers.TryAdd(index, server);
        }
    }
}