using System;

namespace Rafty.Concensus
{
    public interface IPeer
    {
        Guid Id {get;}
        RequestVoteResponse Request(RequestVote requestVote);
        AppendEntriesResponse Request(AppendEntries appendEntries);
        Response<T> Request<T>(T command);
    }
}