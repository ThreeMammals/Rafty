namespace Rafty.Log
{
    public interface ILog
    {
        void Apply(LogEntry logEntry);
        long LastLogIndex {get;}
        long LastLogTerm {get;}
        long TermAtIndex(long index);
    }
}