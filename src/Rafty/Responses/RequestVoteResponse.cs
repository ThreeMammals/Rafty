using System;
using Rafty.Messages;

namespace Rafty.Responses
{
    public class RequestVoteResponse : Message
    {
        public RequestVoteResponse(int term, bool voteGranted, Guid candidateId, Guid voterId)
        {
            Term = term;
            VoteGranted = voteGranted;
            CandidateId = candidateId;
            VoterId = voterId;
        }

        public int Term { get; private set; }
        public bool VoteGranted { get; private set; }
        public Guid CandidateId { get; private set; }
        public Guid VoterId { get; private set; }
    }
}