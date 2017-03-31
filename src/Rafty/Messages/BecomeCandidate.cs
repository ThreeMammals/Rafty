using System;

namespace Rafty.Messages
{
    public class BecomeCandidate : Message
    {
        public BecomeCandidate(Guid lastAppendEntriesMessageIdFromLeader)
        {
            this.LastAppendEntriesMessageIdFromLeader = lastAppendEntriesMessageIdFromLeader;
        }
        public Guid LastAppendEntriesMessageIdFromLeader { get; private set; }
    }
}