namespace Rafty.Concensus
{
    public sealed class AppendEntriesResponse
    {
        public AppendEntriesResponse(long term, bool success)
        {
            Term = term;
            Success = success;
        }

        // currentTerm, for leader to update itself
        public long Term {get;private set;}
        //true if follower contained entry matching prevLogIndex and prevLogTerm
        public bool Success {get;private set;}
    }
}