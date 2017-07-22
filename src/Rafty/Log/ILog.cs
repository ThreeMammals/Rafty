namespace Rafty.Log
{
    public interface ILog
    {
        void Apply(LogEntry logEntry);
        long LastLogIndex {get;}
        long LastLogTerm {get;}
        long GetTermAtIndex(long index);
        void DeleteConflictsFromThisLog(LogEntry logEntry);
    }
}