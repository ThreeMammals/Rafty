using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public class CurrentState
    {
        public CurrentState(Guid id, List<IPeer> peers, long currentTerm, Guid votedFor)
        {
            Id = id;
            CurrentTerm = currentTerm;
            Peers = peers;
            VotedFor = votedFor;
        }

        public long CurrentTerm { get; private set; }
        public Guid VotedFor { get; private set; }
        public long CommitIndex { get; private set; }
        public long LastApplied { get; private set; }
        public Uri Address { get; private set; }
        public Guid Id { get; private set; }
        public List<IPeer> Peers { get; private set; }
    }
}