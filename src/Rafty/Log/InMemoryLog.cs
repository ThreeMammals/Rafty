namespace Rafty.Log
{
    using System;
    using System.Collections.Generic;

    public class InMemoryLog : ILog
    {
        private readonly List<LogEntry> _log;

        public InMemoryLog()
        {
            _log = new List<LogEntry>();
        }

        public long LastLogIndex
        {
            get
            {
                if(_log.Count == 0)
                {
                    return 0;
                }

                return _log.Count - 1;
            }
        }

        public long LastLogTerm
        {
            get
            {
                if(_log.Count == 0)
                {
                    return 0;
                }
                
                var lastLog = _log[_log.Count - 1];
                return lastLog.Term;
            }
        }
        
        public void Apply(LogEntry logEntry)
        {
            _log.Add(logEntry);
        }

        public long TermAtIndex(long index)
        {
            if(_log.Count == 0)
            {
                return 0;
            }

            if(index > _log.Count)
            {
                return 0;
            }

            //todo - fix?
            int i = Convert.ToInt32(index);
            return _log[i].Term;
        }
    }
}