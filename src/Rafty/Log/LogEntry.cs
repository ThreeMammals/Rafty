using Rafty.FiniteStateMachine;

namespace Rafty.Log
{
    using System;

    public class LogEntry
    {
        public LogEntry(ICommand commandData, Type type, long term)
        {
            CommandData = commandData;
            Type = type;
            Term = term;
        }

        public ICommand CommandData { get; private set; }
        public Type Type { get; private set; }
        public long Term { get; private set; }
    }
}