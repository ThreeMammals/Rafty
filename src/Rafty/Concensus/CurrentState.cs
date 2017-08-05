namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using Rafty.Log;

    public class CurrentState
    {
        public CurrentState(Guid id, long currentTerm, Guid votedFor, int commitIndex, int lastApplied, int minTimeout, int maxTimeout)
        {
            Id = id;
            CurrentTerm = currentTerm;
            VotedFor = votedFor;
            CommitIndex = commitIndex;
            LastApplied = lastApplied;
            MinTimeout = minTimeout;
            MaxTimeout = maxTimeout;
        }

        public long CurrentTerm { get; private set; }
        public Guid VotedFor { get; private set; }
        public int CommitIndex { get; private set; }
        public int LastApplied { get; private set; }
        public Uri Address { get; private set; }
        public Guid Id { get; private set; }
        public int MinTimeout { get; private set; }
        public int MaxTimeout { get; private set; }
    }
}