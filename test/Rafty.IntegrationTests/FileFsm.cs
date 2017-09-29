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
        private Guid _id;
        public FileFsm(NodeId nodeId)
        {
            _id = nodeId.Id;
        }
        
        public void Handle(LogEntry log)
        {
            var json = JsonConvert.SerializeObject(log.CommandData);
            File.AppendAllText(_id.ToString(), json);
        }
    }
}
