using System;
using System.Net.Http;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;

namespace Rafty.AcceptanceTests
{
    public class NodePeer : IPeer
    {
        private Node _node;

        public Guid Id 
        {
            get 
            {
                if(_node?.State?.CurrentState?.Id != null)
                {
                    return _node.State.CurrentState.Id;
                }
                
                return default(Guid);
            }
        }

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

        public Response<T> Request<T>(T command) where T : ICommand
        {
            try
            {
                return _node.Accept(command);
            }
            catch(Exception e)
            {
                return new ErrorResponse<T>("Unable to send command to node.", command);
            }
        }
    }
}