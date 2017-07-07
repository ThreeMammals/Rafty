namespace Rafty.Log
{
    public interface ILog
    {
        void Apply(LogEntry logEntry);
    }
}