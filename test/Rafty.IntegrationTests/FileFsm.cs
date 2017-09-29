using System;
using System.IO;
using Newtonsoft.Json;
using Rafty.FiniteStateMachine;
using Rafty.Infrastructure;

namespace Rafty.IntegrationTests
{
    public class FileFsm : IFiniteStateMachine
    {
        private Guid _id;
        public FileFsm(NodeId nodeId)
        {
            _id = nodeId.Id;
        }
        
        public void Handle<T>(T command)
        {
            var json = JsonConvert.SerializeObject(command);
            File.AppendAllText(_id.ToString(), json);
        }
    }
}
