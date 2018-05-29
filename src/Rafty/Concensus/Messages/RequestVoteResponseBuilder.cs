namespace Rafty.Concensus.Messages
{
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