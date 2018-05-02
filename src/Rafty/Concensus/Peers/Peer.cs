using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    using System;
    using System.Threading.Tasks;

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