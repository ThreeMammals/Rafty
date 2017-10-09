using System.Linq;

namespace Rafty.Log
{
    using System;
    using System.Collections.Generic;
    using Rafty.Infrastructure;

    public class InMemoryLog : ILog
    {
        private readonly Dictionary<int, LogEntry> _log;

        public InMemoryLog()
        {
            _log = new Dictionary<int, LogEntry>();
        }

        public Dictionary<int, LogEntry> ExposedForTesting => _log;

        public List<(int index, LogEntry logEntry)> GetFrom(int index)
        {
            var logsToReturn = new List<(int, LogEntry)>();

            foreach(var log in _log)
            {
                if(log.Key >= index)
                {
                    logsToReturn.Add((log.Key, log.Value));
                }
            }

            return logsToReturn;
        }

        public int LastLogIndex
        {
            get
            {
                var lastLog = _log.LastOrDefault();

                if(lastLog.Value != null)
                {
                    return lastLog.Key;
                }

                return 1;
            }
        }

        public long LastLogTerm
        {
            get
            {
                 var lastLog = _log.LastOrDefault();

                if(lastLog.Value != null)
                {
                    return lastLog.Value.Term;
                }

                return 0;
            }
        }
        
        public int Apply(LogEntry logEntry)
        {
            if(_log.Count <= 0)
            {
                _log.Add(1, logEntry);
                return 1;
            }
            else
            {
                var nextIndex = _log.Max(x => x.Key) + 1;
                _log.Add(nextIndex, logEntry);
                return nextIndex;
            }
        }

        public long GetTermAtIndex(int index)
        {
            if(_log.Count == 0)
            {
                return 0;
            }

            if(index > _log.Count)
            {
                return 0;
            }

            if (index <= 0)
            {
                throw new Exception("Log starts at 1...");
            }

            return _log[index].Term;
        }

        public void DeleteConflictsFromThisLog(int logIndex, LogEntry logEntry)
        {
            if(logIndex > 1 && logIndex > _log.Count -1)
            {
                return;
            }

            for (int i = logIndex; i <= _log.Max(x => x.Key); i++)
            {
                var match = _log[logIndex];
                if (match.Term != logEntry.Term)
                {
                    var toRemove = _log.Max(x => x.Key) - i;
                    RemoveRange(i, toRemove);
                    break;
                }
            }
        }

        private void RemoveRange(int indexToRemove, int toRemove)
        {
            while(_log.ContainsKey(indexToRemove))
            {
                _log.Remove(indexToRemove);
                indexToRemove++;
            }
        }

        public int Count => _log.Count;

        public LogEntry Get(int index)
        {
            if (index <= 0)
            {
                throw new Exception("Log starts at 1...");
            }

            if(_log.Count >= index)
            {
                return _log[index];
            }

            throw new Exception("Nothing in log..");
        }

        public void Remove(int indexOfCommand)
        {
            _log.Remove(indexOfCommand);
        }
    }
}