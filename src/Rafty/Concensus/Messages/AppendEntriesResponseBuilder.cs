namespace Rafty.Concensus.Messages
{
    public class AppendEntriesResponseBuilder
    {
        private long _term;
        private bool _success;

        public AppendEntriesResponseBuilder WithTerm(long term)
        {
            _term = term;
            return this;
        }

        public AppendEntriesResponseBuilder WithSuccess(bool success)
        {
            _success = success;
            return this;
        }

        public AppendEntriesResponse Build()
        {
            return new AppendEntriesResponse(_term, _success);
        }
    }
}