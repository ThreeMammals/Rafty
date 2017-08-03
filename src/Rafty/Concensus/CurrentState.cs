namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using Rafty.Log;

    public class CurrentState
    {
        public CurrentState(Guid id, long currentTerm, Guid votedFor, TimeSpan timeout, int commitIndex, int lastApplied)
        {
            Id = id;
            CurrentTerm = currentTerm;
            VotedFor = votedFor;
            Timeout = timeout;
            CommitIndex = commitIndex;
            LastApplied = lastApplied;
        }

        public long CurrentTerm { get; private set; }
        public Guid VotedFor { get; private set; }
        public int CommitIndex { get; private set; }
        public int LastApplied { get; private set; }
        public Uri Address { get; private set; }
        public Guid Id { get; private set; }
        public TimeSpan Timeout { get; private set; }
    }
}