# Rafty

Please note rafty is experimental

Rafty is an implementation of the Raft concensus algorythm [see here](https://raft.github.io/) created using C# and .NET core. Rafty is the algorythm only and does not provide useful implementation of the transport between nodes, the state machine or log. Instead Rafty provides interfaces that you will need to implement. I reccomend at least 5 nodes in your cluster for Rafty to operate optimally and this is basically all I've tested....

Rafty was built to allow Ocelot (another project of mine) to run in a cluster without a database or relying on another piece of software to persist state. This will also allow me to turn Ocelot into a service discovery provider and key value store so its pretty nice....if it works!

## Install

Bring the rafty package into your project using nuget.

```Install-Package Rafty```

This will make all the raft code available to you!

## ILog

You must implement ILog which provides a description of each member in the summary comments of the interface. This log implementation should be persistant and not shared between nodes.

## IFiniteStateMachine

You must implement IFiniteStateMachine which provides a description of each member in the summary comments of the interface. This just takes a command of T and you execute it! Pretty simple.

## IPeer

You must implement IPeer which provides a description of each member in the summary comments of the interface. This encapsulates the transport logic to each node in the cluster.

## IPeersProvider

You must implement IPeersProvider which provides a description of each member in the summary comments of the interface. This should return all available nodes in the cluster as peers.

## ISettings

You must implement ISettings which provides a description of each member in the summary comments of the interface. Holds some basic settings information that you might want to tweak such as timeout.

## Startup

Rafty provides some in memory implementations of its interfaces (you shouldn't use these for anything serious).

```csharp
    var log = new InMemoryLog();
    var fsm = new InMemoryStateMachine();
    var settings = new InMemorySettings(1000, 3500, 50, 5000);
    var peersProvider = new InMemoryPeersProvider(_peers);
    var node = new Node(fsm, log, settings, peersProvider);
    node.Start();
```

The above code will get a Rafty node up and running. If the IPeersProvider does not return any IPeers then it will elect itself leader and just run along happily. If something joins the cluster later it will update that new node as the node will get a heartbeat before it can elect itself. Or an election will start!

So in order to get Rafty really running the IPeerProvider needs to return peers. The easiest way to do this is just provide a HTTP transport version of IPeer. The host and port of these peers is known to each node and you just push them into memory and return them. The request will then flow through the IPeer interfaces from the nodes!

Finally you need to expose the INode interface to some kind of HTTP. I would advise just a plain old .net core web api type thing. These are the methods you need to expose and the transport in your IPeer should hit these URLS (hope that makes some sense). You can look at NodePeer to see how I do this in memory.

```csharp
AppendEntriesResponse Request(AppendEntries appendEntries);
RequestVoteResponse Request(RequestVote requestVote);
Response<T> Request<T>(T command);
```

## Further help..

The Acceptance and Integration tests will be helpful for anyone who wants to use Rafty.

## Future

I will provide some half decent implementations of these interfaces so you don't have to.
