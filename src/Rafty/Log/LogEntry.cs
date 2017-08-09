namespace Rafty.Log
{
    using System;

    public class LogEntry
    {
        public LogEntry(string commandData, Type type, long term)
        {
            CommandData = commandData;
            Type = type;
            Term = term;
        }

        public string CommandData { get; private set; }
        public Type Type { get; private set; }
        public long Term { get; private set; }
    }
}