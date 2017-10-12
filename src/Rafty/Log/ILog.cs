using System;
using System.Collections.Generic;

namespace Rafty.Log
{
    public interface ILog
    {
        /// <summary>
        /// This will apply a log entry and return its index
        /// </summary>
        int Apply(LogEntry log);
        /// <summary>
        /// This will return the log entry at the index passed in
        /// </summary>
        LogEntry Get(int index);
        /// <summary>
        /// This will return all the log entries from a certain point based on index including the first match on the index passed in
        /// </summary>
        List<(int index, LogEntry logEntry)> GetFrom(int index);
        /// <summary>
        /// This will return the last known log index or 1
        /// </summary>
        int LastLogIndex {get;}
        /// <summary>
        /// This will return the last know log term or 0
        /// </summary>
        long LastLogTerm {get;}
        /// <summary>
        /// This will get the term at the index passed in
        /// </summary>
        long GetTermAtIndex(int index);
        /// <summary>
        /// This will delete any conflicts from the log, if the log entry passed in doesnt match the log entry
        //in the log for the given index it will also delete any further logs
        /// </summary>
        void DeleteConflictsFromThisLog(int index, LogEntry logEntry);
        /// <summary>
        /// This returns a count of the logs
        /// </summary>
        int Count { get; }
        /// <summary>
        /// This removes the command at the index passed in.
        /// </summary>
        void Remove(int indexOfCommand);
    }
} 