using System;
using System.Collections.Generic;

namespace Rafty.Log
{
    public interface ILog
    {
        int Apply(LogEntry log);
        LogEntry Get(int index);
        List<Tuple<int, LogEntry>> GetFrom(int index);
        int LastLogIndex {get;}
        long LastLogTerm {get;}
        long GetTermAtIndex(int index);
        void DeleteConflictsFromThisLog(int index, LogEntry logEntry);
        int Count { get; }
    }
} 