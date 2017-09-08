namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using Log;

    public class AppendEntriesBuilder
    {
        private List<LogEntry> _entries;
        private int _leaderCommitIndex;
        private Guid _leaderId;
        private int _previousLogIndex;
        private long _previousLogTerm;
        private long _term;

        public AppendEntriesBuilder WithTerm(long term)
        {
            _term = term;
            return this;
        }

        public AppendEntriesBuilder WithLeaderId(Guid leaderId)
        {
            _leaderId = leaderId;
            return this;
        }

        public AppendEntriesBuilder WithPreviousLogIndex(int previousLogIndex)
        {
            _previousLogIndex = previousLogIndex;
            return this;
        }

        public AppendEntriesBuilder WithPreviousLogTerm(long previousLogTerm)
        {
            _previousLogTerm = previousLogTerm;
            return this;
        }

        public AppendEntriesBuilder WithEntries(List<LogEntry> entries)
        {
            _entries = entries;
            return this;
        }

        public AppendEntriesBuilder WithLeaderCommitIndex(int leaderCommitIndex)
        {
            _leaderCommitIndex = leaderCommitIndex;
            return this;
        }

        public AppendEntriesBuilder WithEntry(LogEntry entry)
        {
            _entries = new List<LogEntry> {entry};
            return this;
        }

        public AppendEntries Build()
        {
            return new AppendEntries(_term, _leaderId, _previousLogIndex, _previousLogTerm, _entries, _leaderCommitIndex);
        }
    }
}