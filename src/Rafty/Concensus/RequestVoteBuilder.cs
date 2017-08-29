using System;

namespace Rafty.Concensus
{
    public class RequestVoteBuilder
    {
        private long _term;
        private Guid _candidateId;
        private long _lastLogIndex;
        private long _lastLogTerm;

        public RequestVoteBuilder WithTerm(long term)
        {
            _term = term;
            return this;
        }

        public RequestVoteBuilder WithCandidateId(Guid candidateId)
        {
            _candidateId = candidateId;
            return this;
        }

        public RequestVoteBuilder WithLastLogIndex(long lastLogIndex)
        {
            _lastLogIndex = lastLogIndex;
            return this;
        }

        public RequestVoteBuilder WithLastLogTerm(long lastLogTerm)
        {
            _lastLogTerm =lastLogTerm;
            return this;
        }

        public RequestVote Build()
        {
            return new RequestVote(_term, _candidateId, _lastLogIndex, _lastLogTerm);
        }
        
    }
}