namespace Rafty.Concensus.Node
{
    using System;
    using System.Threading.Tasks;
    using FiniteStateMachine;
    using Infrastructure;
    using Messages;
    using Peers;

    public class NodePeer : IPeer
    {
        private Node _node;

        public string Id => _node?.State?.CurrentState?.Id;

        public void SetNode (Node node)
        {
            _node = node;
        }

        public async Task<RequestVoteResponse> Request(RequestVote requestVote)
        {
            try
            {
                return await _node.Handle(requestVote);
            }
            catch(Exception e)
            {
                return new RequestVoteResponse(false, 0);
            }
        }

        public async Task<AppendEntriesResponse> Request(AppendEntries appendEntries)
        {
            try
            {
                return await _node.Handle(appendEntries);
            }
            catch(Exception e)
            {
                return new AppendEntriesResponse(0, false);
            }
        }

        public async Task<Response<T>> Request<T>(T command) where T : ICommand
        {
            try
            {
                return await _node.Accept(command);
            }
            catch(Exception e)
            {
                return new ErrorResponse<T>("Unable to send command to node.", command);
            }
        }
    }
}