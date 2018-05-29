namespace Rafty.Concensus.Messages
{
    public sealed class AppendEntriesResponse
    {
        public AppendEntriesResponse(long term, bool success)
        {
            Term = term;
            Success = success;
        }

        /// <summary>
        // CurrentTerm, for leader to update itself.
        /// </summary>
        public long Term {get;private set;}

        /// <summary>
        // True if follower contained entry matching prevLogIndex and prevLogTerm.
        /// </summary>
        public bool Success {get;private set;}
    }
}