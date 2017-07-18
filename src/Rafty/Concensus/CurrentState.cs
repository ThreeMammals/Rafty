namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using Rafty.Log;

    public class CurrentState
    {
        public CurrentState(Guid id, List<IPeer> peers, long currentTerm, Guid votedFor, TimeSpan timeout, ILog log)
        {
            Id = id;
            CurrentTerm = currentTerm;
            Peers = peers;
            VotedFor = votedFor;
            Timeout = timeout;
            Log = log;
        }

        public long CurrentTerm { get; private set; }
        public Guid VotedFor { get; private set; }
        public long CommitIndex { get; private set; }
        public long LastApplied { get; private set; }
        public Uri Address { get; private set; }
        public Guid Id { get; private set; }
        public List<IPeer> Peers { get; private set; }
        public TimeSpan Timeout { get; private set; }
        public ILog Log {get;private set;}
    }
}