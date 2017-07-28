using System.Collections.Generic;

namespace Rafty.Log
{
    public interface ILog
    {
        void Apply(LogEntry log);
        LogEntry Get(int index);
        List<LogEntry> GetFrom(int index);
        int LastLogIndex {get;}
        long LastLogTerm {get;}
        long GetTermAtIndex(int index);
        void DeleteConflictsFromThisLog(LogEntry logEntry);
        int Count { get; }
    }
} 