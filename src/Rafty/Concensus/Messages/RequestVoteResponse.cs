namespace Rafty.Concensus
{
    public sealed class RequestVoteResponse
    {
        public RequestVoteResponse(bool voteGranted, long term)
        {
            VoteGranted = voteGranted;
            Term = term;
        }

        /// <summary>
        // True means candidate received vote.
        /// </summary>
        public bool VoteGranted { get; private set; }

        /// <summary>
        // CurrentTerm, for candidate to update itself.
        /// </summary>
        public long Term {get;private set;}
    }
}