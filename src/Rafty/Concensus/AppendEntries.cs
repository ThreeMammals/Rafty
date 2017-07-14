namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using Log;

    public sealed class AppendEntries : Message
    {
        public AppendEntries(long term, Guid leaderId, long previousLogIndex, long previousLogTerm,
            List<LogEntry> entries, long leaderCommitIndex)
            : base(Guid.NewGuid())
        {
            Term = term;
            LeaderId = leaderId;
            PreviousLogIndex = previousLogIndex;
            PreviousLogTerm = previousLogTerm;
            Entries = entries;
            LeaderCommitIndex = leaderCommitIndex;
        }

        // term leader’s term
        public long Term { get; private set; }
        // leaderId so follower can redirect clients
        public Guid LeaderId { get; private set; }
        // prevLogIndex index of log entry immediately preceding new ones
        public long PreviousLogIndex { get; private set; }
        // prevLogTerm term of prevLogIndex entry
        public long PreviousLogTerm { get; private set; }
        // entries[] log entries to store (empty for heartbeat may send more than one for efficiency)
        public List<LogEntry> Entries { get; private set; }
        // leaderCommit leader’s commitIndex
        public long LeaderCommitIndex { get; private set; }
    }
}