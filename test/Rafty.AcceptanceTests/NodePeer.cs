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
            try
            {
                return _node.Handle(requestVote);
            }
            catch(Exception e)
            {
                return new RequestVoteResponse(false, 0);
            }
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            try
            {
                return _node.Handle(appendEntries);
            }
            catch(Exception e)
            {
                return new AppendEntriesResponse(0, false);
            }
        }
    }
}