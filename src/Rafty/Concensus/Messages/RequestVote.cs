using System;

namespace Rafty.Concensus
{
    public sealed class RequestVote
    {
        public RequestVote(long term, Guid candidateId, long lastLogIndex, long lastLogTerm)
        {
            Term = term;
            CandidateId = candidateId;
            LastLogIndex = lastLogIndex;
            LastLogTerm = lastLogTerm;
        }
        
        //term candidate’s term
        public long Term {get;private set;}
        //candidateId candidate requesting vote
        public Guid CandidateId {get;private set;}
        //lastLogIndex index of candidate’s last log entry (§5.4)
        public long LastLogIndex {get;private set;}
        //lastLogTerm term of candidate’s last log entry (§5.4)        
        public long LastLogTerm {get;private set;}
    }
}