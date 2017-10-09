using System;
using System.Collections.Generic;

namespace Rafty.Log
{
    public interface ILog
    {
        //This will apply a log entry and return its index
        int Apply(LogEntry log);
        //This will return the log entry at the index passed in
        LogEntry Get(int index);
        //This will return all the log entries from a certain point based on index including the first match on the index passed in
        List<(int index, LogEntry logEntry)> GetFrom(int index);
        //This will return the last known log index or 1
        int LastLogIndex {get;}
        //This will return the last know log term or 0
        long LastLogTerm {get;}
        //This will get the term at the index passed in
        long GetTermAtIndex(int index);
        //This will delete any conflicts from the log, if the log entry passed in doesnt match the log entry
        //in the log for the given index it will also delete any further logs
        void DeleteConflictsFromThisLog(int index, LogEntry logEntry);
        //This returns a count of the logs
        int Count { get; }
        //This removes the command at the index passed in.
        void Remove(int indexOfCommand);
    }
} 