namespace Rafty.Concensus
{
    public sealed class RequestVoteResponse
    {
        public RequestVoteResponse(bool grant, long term)
        {
            VoteGranted = grant;
            Term = term;
        }

        //true means candidate received vote
        public bool VoteGranted { get; private set; }
        //currentTerm, for candidate to update itself
        public long Term {get;private set;}
    }
}