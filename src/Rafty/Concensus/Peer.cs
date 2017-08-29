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
    }
}