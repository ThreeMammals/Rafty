namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using Log;

    public sealed class AppendEntries : Message
    {
        public AppendEntries(long term, string leaderId, int previousLogIndex, long previousLogTerm,
            List<LogEntry> entries, int leaderCommitIndex)
            : base(Guid.NewGuid())
        {
            Term = term;
            LeaderId = leaderId;
            PreviousLogIndex = previousLogIndex;
            PreviousLogTerm = previousLogTerm;
            Entries = entries ?? new List<LogEntry>();
            LeaderCommitIndex = leaderCommitIndex;
        }

        /// <summary>
        // Term leader’s term.
        /// </summary>
        public long Term { get; private set; }

        /// <summary>
        // LeaderId so follower can redirect clients.
        /// </summary>
        public string LeaderId { get; private set; }

        /// <summary>
        // PrevLogIndex index of log entry immediately preceding new ones.
        /// </summary>
        public int PreviousLogIndex { get; private set; }

        /// <summary>
        // PrevLogTerm term of prevLogIndex entry.
        /// </summary>
        public long PreviousLogTerm { get; private set; }

        /// <summary>
        // Entries[] log entries to store (empty for heartbeat may send more than one for efficiency).
        /// </summary>
        public List<LogEntry> Entries { get; private set; }

        /// <summary>
        // LeaderCommit leader’s commitIndex.
        /// </summary>
        public int LeaderCommitIndex { get; private set; }
    }
}