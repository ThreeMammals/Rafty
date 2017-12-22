using System;
using System.IO;
using Newtonsoft.Json;
using Rafty.FiniteStateMachine;
using Rafty.Infrastructure;
using Rafty.Log;

namespace Rafty.IntegrationTests
{
    public class FileFsm : IFiniteStateMachine
    {
        private string _id;
        public FileFsm(NodeId nodeId)
        {
            _id = nodeId.Id;
        }
        
        public void Handle(LogEntry log)
        {
            try
            {
                var json = JsonConvert.SerializeObject(log.CommandData);
                File.AppendAllText(_id.Replace("/","").Replace(":","").ToString(), json);
            }
            catch(Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}
