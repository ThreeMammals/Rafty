using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rafty.FiniteStateMachine;
using Rafty.Infrastructure;
using Rafty.Log;

namespace Rafty.IntegrationTests
{
    public class FileFsm : IFiniteStateMachine
    {
        private readonly string _id;
        private readonly string _path;
        private SemaphoreSlim _lock = new SemaphoreSlim(1,1);
        private JsonSerializerSettings _settings;
        private ILogger _logger;

        public FileFsm(NodeId nodeId, ILoggerFactory factory)
        {
            _logger = factory.CreateLogger<FileFsm>();

            _id = nodeId.Id;

            _path = _id.Replace("/","").Replace(":","").ToString();

            _settings = new JsonSerializerSettings() { 
                TypeNameHandling = TypeNameHandling.All
            };

            try
            {
                _lock.Wait();

                if (!File.Exists(_path))
                {
                    using (FileStream fs = File.Create(_path))
                    {
                        Byte[] info = new UTF8Encoding().GetBytes("");

                        fs.Write(info, 0, info.Length);
                    }
                }
            }
            catch(Exception)
            {
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task Handle(LogEntry log)
        {
            try
            {
                _logger.LogInformation($"id: {_id} applying log to state machine log.Term: {log.Term}");
                await _lock.WaitAsync();

                var current = await File.ReadAllTextAsync(_path);

                var logEntries = JsonConvert.DeserializeObject<List<ICommand>>(current, _settings);

                if(logEntries == null)
                {
                    logEntries = new List<ICommand>();
                }

                logEntries.Add(log.CommandData);

                var next = JsonConvert.SerializeObject(logEntries, _settings);

                await File.WriteAllTextAsync(_path, next);
            }
            catch(Exception e)
            {
                _logger.LogInformation($"id: {_id} threw an exception when trying to handle log entry, exception: {e}");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
