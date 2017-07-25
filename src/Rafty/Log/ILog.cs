namespace Rafty.Log
{
    public interface ILog
    {
        void Apply(LogEntry log);
        LogEntry Get(long index);
        long LastLogIndex {get;}
        long LastLogTerm {get;}
        long GetTermAtIndex(long index);
        void DeleteConflictsFromThisLog(LogEntry logEntry);
    }
}