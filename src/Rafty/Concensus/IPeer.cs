namespace Rafty.Concensus
{
    public interface IPeer
    {
        RequestVoteResponse Request(RequestVote requestVote);
        AppendEntriesResponse Request(AppendEntries appendEntries);
    }
}