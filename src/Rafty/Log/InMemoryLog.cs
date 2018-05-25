using System.Linq;

namespace Rafty.Log
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Rafty.Infrastructure;

    public class InMemoryLog : ILog
    {
        private readonly Dictionary<int, LogEntry> _log;

        public InMemoryLog()
        {
            _log = new Dictionary<int, LogEntry>();
        }

        public Dictionary<int, LogEntry> ExposedForTesting => _log;

        public Task<List<(int index, LogEntry logEntry)>> GetFrom(int index)
        {
            var logsToReturn = new List<(int, LogEntry)>();

            foreach(var log in _log)
            {
                if(log.Key >= index)
                {
                    logsToReturn.Add((log.Key, log.Value));
                }
            }

            return Task.FromResult(logsToReturn);
        }

        public Task<int> LastLogIndex()
        {
                var lastLog = _log.LastOrDefault();

                if(lastLog.Value != null)
                {
                    return Task.FromResult(lastLog.Key);
                }

                return Task.FromResult(1);
        }

        public Task<long> LastLogTerm()
        {
                var lastLog = _log.LastOrDefault();

                if(lastLog.Value != null)
                {
                    return Task.FromResult(lastLog.Value.Term);
                }

                return Task.FromResult((long)0);
        }
        
        public Task<int> Apply(LogEntry logEntry, ILogger logger, string id)
        {
            if(_log.Count <= 0)
            {
                _log.Add(1, logEntry);
                return Task.FromResult(1);
            }
            else
            {
                var nextIndex = _log.Max(x => x.Key) + 1;
                _log.Add(nextIndex, logEntry);
                return Task.FromResult(nextIndex);
            }
        }

        public Task<long> GetTermAtIndex(int index)
        {
            if(_log.Count == 0)
            {
                return Task.FromResult((long)0);
            }

            if(index > _log.Count)
            {
                return Task.FromResult((long)0);
            }

            if (index <= 0)
            {
                return Task.FromResult((long)0);
            }

            return Task.FromResult(_log[index].Term);
        }

        public Task DeleteConflictsFromThisLog(int logIndex, LogEntry logEntry, ILogger logger, string id)
        {
            if((logIndex > 1 && logIndex > _log.Count -1) || _log.Count == 0 || logIndex == 0)
            {
                return Task.CompletedTask;;
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

            return Task.CompletedTask;
        }

        public async Task<bool> IsDuplicate(int logIndex, LogEntry logEntry)
        {
            if((logIndex > 1 && logIndex > _log.Count -1) || _log.Count == 0)
            {
                return false;
            }

            for (int i = logIndex; i <= _log.Max(x => x.Key); i++)
            {
                var match = _log[logIndex];
                if(logEntry.Term == match.Term)
                {
                    return true;
                }
            }

            return false;
        }

        private Task RemoveRange(int indexToRemove, int toRemove)
        {
            while(_log.ContainsKey(indexToRemove))
            {
                _log.Remove(indexToRemove);
                indexToRemove++;
            }

            return Task.CompletedTask;
        }

        public Task<int> Count() => Task.FromResult(_log.Count);

        public Task<LogEntry> Get(int index)
        {
            if (index <= 0)
            {
                throw new Exception("Log starts at 1...");
            }

            if(_log.Count >= index)
            {
                return Task.FromResult(_log[index]);
            }

            throw new Exception("Nothing in log..");
        }

        public Task Remove(int indexOfCommand, ILogger logger, string id)
        {
            _log.Remove(indexOfCommand);
            return Task.CompletedTask;
        }
    }
}