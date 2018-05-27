namespace Rafty.Concensus.Peers
{
    using System;
    using System.Threading.Tasks;
    using FiniteStateMachine;
    using Infrastructure;
    using Messages;

    public class Peer : IPeer
    {
        public string Id => throw new NotImplementedException();

        public Task<RequestVoteResponse> Request(RequestVote requestVote)
        {
            throw new NotImplementedException();
        }

        public Task<AppendEntriesResponse> Request(AppendEntries appendEntries)
        {
            throw new NotImplementedException();
        }

        public Task<Response<T>> Request<T>(T command) where T : ICommand
        {
            throw new NotImplementedException();
        }
    }
}