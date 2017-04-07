using Microsoft.AspNetCore.Hosting;
using Rafty.Messaging;
using Rafty.Raft;
using Rafty.ServiceDiscovery;
using Rafty.State;

namespace Rafty.AcceptanceTests
{
    public class ServerContainer
    {
        public ServerContainer(IWebHost webHost, Server server, string serverUrl, IMessageSender messageSender, ServerInCluster serverInCluster, IMessageBus messageBus, IStateMachine stateMachine)
        {
            StateMachine = stateMachine;
            MessageBus = messageBus;
            WebHost = webHost;
            Server = server;
            ServerUrl = serverUrl;
            MessageSender = messageSender;
            ServerInCluster = serverInCluster;
        }

        public IWebHost WebHost { get; private set; }
        public Server Server { get; private set; }
        public string ServerUrl { get; private set; }
        public IMessageSender MessageSender { get; private set; }
        public ServerInCluster ServerInCluster {get;private set;}
        public IMessageBus MessageBus {get;private set;}
        public IStateMachine StateMachine {get;private set;}
    }
}