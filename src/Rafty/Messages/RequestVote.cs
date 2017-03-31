using System;

namespace Rafty.Messages
{
    public class RequestVote : Message
    {
        public RequestVote(int term, Guid candidateId, int lastLogIndex, int lastLogTerm, Guid voterId)
        {
            Term = term;
            CandidateId = candidateId;
            LastLogIndex = lastLogIndex;
            LastLogTerm = lastLogTerm;
            VoterId = voterId;
        }

        public int Term { get; private set; }
        public Guid CandidateId { get; private set; }
        public int LastLogIndex { get; private set; }
        public int LastLogTerm { get; private set; }
        public Guid VoterId { get; private set; }
    }
}