namespace Rafty.Log
{
    using System;

    public class LogEntry
    {
        public LogEntry(string commandData, Type type, long term, int currentCommitIndex)
        {
            CommandData = commandData;
            Type = type;
            Term = term;
            CurrentCommitIndex = currentCommitIndex;
        }

        public string CommandData { get; private set; }
        public Type Type { get; private set; }
        public long Term { get; private set; }
        public int CurrentCommitIndex { get; private set; }
    }
}