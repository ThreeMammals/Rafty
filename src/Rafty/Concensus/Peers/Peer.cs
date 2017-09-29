using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    using System;

    public class Peer : IPeer
    {
        public Guid Id => throw new NotImplementedException();

        public RequestVoteResponse Request(RequestVote requestVote)
        {
            throw new NotImplementedException();
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            throw new NotImplementedException();
        }

        public Response<T> Request<T>(T command) where T : ICommand
        {
            throw new NotImplementedException();
        }
    }
}