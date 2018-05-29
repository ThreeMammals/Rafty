namespace Rafty.Concensus.States
{
    using System;

    public class CurrentState
    {
        public CurrentState(string id, long currentTerm, string votedFor, int commitIndex, int lastApplied, string leaderId)
        {
            Id = id;
            CurrentTerm = currentTerm;
            VotedFor = votedFor;
            CommitIndex = commitIndex;
            LastApplied = lastApplied;
            LeaderId = leaderId;
        }

        public long CurrentTerm { get; private set; }
        public string VotedFor { get; private set; }
        public int CommitIndex { get; private set; }
        public int LastApplied { get; private set; }
        public Uri Address { get; private set; }
        public string Id { get; private set; }
        public string LeaderId { get; private set; }
    }
}