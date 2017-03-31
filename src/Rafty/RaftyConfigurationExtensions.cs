using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Newtonsoft.Json;

namespace Rafty
{
    public static class RaftyConfigurationExtensions
    {

        public static IApplicationBuilder UseRafty(this IApplicationBuilder builder, 
            Uri baseUri, 
            IMessageSender messageSender, 
            IMessageBus messageBus, 
            IStateMachine stateMachine, 
            IServiceRegistry serviceRegistry, 
            ILogger logger,
            List<ServerInCluster> remoteServers,
            string raftyBasePath = null)
        {
            builder.UseRaftyForTesting(baseUri, messageSender, messageBus, stateMachine, serviceRegistry,
                logger, remoteServers, raftyBasePath);

            return builder;
        }

        public static (IApplicationBuilder builder, Server server, ServerInCluster serverInCluster) UseRaftyForTesting(this IApplicationBuilder builder,
           Uri baseUri,
           IMessageSender messageSender,
           IMessageBus messageBus,
           IStateMachine stateMachine,
           IServiceRegistry serviceRegistry,
           ILogger logger,
           List<ServerInCluster> remoteServers,
           string raftyBasePath = null)
        {
            var urlConfig = RaftyUrlConfig.Get(raftyBasePath);

            var server = new Server(messageBus, remoteServers, stateMachine, logger);

            serviceRegistry.Register(new RegisterService(RaftyServiceDiscoveryName.Get(), server.Id, baseUri));

            messageSender.SetServer(server);

            var serverInCluster = new ServerInCluster(server.Id);

            remoteServers.Add(serverInCluster);

            builder.Map(urlConfig.appendEntriesUrl, app =>
            {
                app.Run(async context =>
                {
                    try
                    {
                        var reader = new StreamReader(context.Request.Body);
                        var content = reader.ReadToEnd();
                        var appendEntries = JsonConvert.DeserializeObject<AppendEntries>(content);
                        var appendEntriesResponse = server.Receive(appendEntries);
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(appendEntriesResponse));
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(new EventId(1), exception, $"There was an error handling {urlConfig.appendEntriesUrl}");
                    }
                });
            });

            builder.Map(urlConfig.requestVoteUrl, app =>
            {
                app.Run(async context =>
                {
                    try
                    {
                        var reader = new StreamReader(context.Request.Body);
                        var content = reader.ReadToEnd();
                        var requestVote = JsonConvert.DeserializeObject<RequestVote>(content);
                        var requestVoteResponse = server.Receive(requestVote);
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(requestVoteResponse));
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(new EventId(1), exception, $"There was an error handling {urlConfig.requestVoteUrl}");
                    }
                });
            });

            builder.Map(urlConfig.commandUrl, app =>
            {
                app.Run(async context =>
                {
                    try
                    {
                        var reader = new StreamReader(context.Request.Body);
                        var content = reader.ReadToEnd();
                        var command = JsonConvert.DeserializeObject<FakeCommand>(content);
                        var sendCommandToLeaderResponse = server.Receive(command);
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(sendCommandToLeaderResponse));
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(new EventId(1), exception, $"There was an error handling {urlConfig.commandUrl}");
                    }
                });
            });
            return (builder, server, serverInCluster);
        }
    }
}
