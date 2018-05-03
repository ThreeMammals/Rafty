using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rafty.FiniteStateMachine;
using Rafty.Infrastructure;
using Rafty.Log;

namespace Rafty.IntegrationTests
{
    public class FileFsm : IFiniteStateMachine
    {
        private readonly string _id;

        public FileFsm(NodeId nodeId)
        {
            _id = nodeId.Id;
        }
        
        public async Task Handle(LogEntry log)
        {
            try
            {
                var json = JsonConvert.SerializeObject(log.CommandData);
                await File.AppendAllTextAsync(_id.Replace("/","").Replace(":","").ToString(), json);
            }
            catch(Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}
