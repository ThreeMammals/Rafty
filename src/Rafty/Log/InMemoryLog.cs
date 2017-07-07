using System.Collections.Generic;
using System.Text;

namespace Rafty.Log
{
    public class InMemoryLog : ILog
    {
        private readonly List<LogEntry> _log;

        public InMemoryLog()
        {
            _log = new List<LogEntry>();
        }

        public void Apply(LogEntry logEntry)
        {
            _log.Add(logEntry);
        }
    }
}
