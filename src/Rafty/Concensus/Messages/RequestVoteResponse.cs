namespace Rafty.Concensus
{
    public sealed class RequestVoteResponse
    {
        public RequestVoteResponse(bool voteGranted, long term)
        {
            VoteGranted = voteGranted;
            Term = term;
        }

        //true means candidate received vote
        public bool VoteGranted { get; private set; }
        //currentTerm, for candidate to update itself
        public long Term {get;private set;}
    }

    public class RequestVoteResponseBuilder
    {
        private bool _voteGranted;
        private long _term;

        public RequestVoteResponseBuilder WithVoteGranted(bool voteGranted)
        {
            _voteGranted = voteGranted;
            return this;
        }

        public RequestVoteResponseBuilder WithTerm(long term)
        {
            _term = term;
            return this;
        }

        public RequestVoteResponse Build()
        {
            return new RequestVoteResponse(_voteGranted, _term);
        }
    }
}