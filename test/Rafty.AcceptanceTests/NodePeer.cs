using System;
using Rafty.Concensus;

namespace Rafty.AcceptanceTests
{
    public class NodePeer : IPeer
    {
        private Node _node;

        public Guid Id => _node.State.CurrentState.Id;

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
}