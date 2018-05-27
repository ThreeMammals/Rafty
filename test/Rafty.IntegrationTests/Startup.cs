using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;
using Rafty.Infrastructure;
using Rafty.Log;
using ConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace Rafty.IntegrationTests
{
    using Concensus.Messages;
    using Concensus.Node;

    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddJsonFile("peers.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var settings = new InMemorySettings(4000, 6000, 500, 10000);
            services.AddSingleton<ILog, SqlLiteLog>();
            services.AddSingleton<IFiniteStateMachine, FileFsm>();
            services.AddSingleton<ISettings>(settings);
            services.AddSingleton<IPeersProvider, FilePeersProvider>();
            services.AddSingleton<INode, Node>();
            services.Configure<FilePeers>(Configuration);
            services.AddLogging();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetime)
        {
            loggerFactory.AddFile("Logs/myapp-{Date}.txt");
            applicationLifetime.ApplicationStopping.Register(() => OnShutdown(app));
            //loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            var webHostBuilder = (IWebHostBuilder)app.ApplicationServices.GetService(typeof(IWebHostBuilder));
            var baseSchemeUrlAndPort = webHostBuilder.GetSetting(WebHostDefaults.ServerUrlsKey);
            var node = (INode)app.ApplicationServices.GetService(typeof(INode));
            var nodeId = (NodeId)app.ApplicationServices.GetService(typeof(NodeId));
            var logger = loggerFactory.CreateLogger<Startup>();
            node.Start(nodeId);

            var jsonSerializerSettings = new JsonSerializerSettings() { 
                TypeNameHandling = TypeNameHandling.All
            };

            app.Run(async context =>
                {
                    try
                    {
                        var n = (INode)context.RequestServices.GetService(typeof(INode));
                        if(context.Request.Path == "/appendentries")
                        {
                            var reader = new StreamReader(context.Request.Body);
                            var content = reader.ReadToEnd();
                            var appendEntries = JsonConvert.DeserializeObject<AppendEntries>(content, jsonSerializerSettings);
                            logger.LogInformation(new EventId(1), null, $"{baseSchemeUrlAndPort}/appendentries called, my state is {n.State.GetType().FullName}");
                            var appendEntriesResponse = await n.Handle(appendEntries);
                            var json = JsonConvert.SerializeObject(appendEntriesResponse);
                            await context.Response.WriteAsync(json);
                            reader.Dispose();
                            return;
                        }

                        if (context.Request.Path == "/requestvote")
                        {
                            var reader = new StreamReader(context.Request.Body);
                            var requestVote = JsonConvert.DeserializeObject<RequestVote>(reader.ReadToEnd(), jsonSerializerSettings);
                            logger.LogInformation(new EventId(2), null, $"{baseSchemeUrlAndPort}/requestvote called, my state is {n.State.GetType().FullName}");
                            var requestVoteResponse = await n.Handle(requestVote);
                            var json = JsonConvert.SerializeObject(requestVoteResponse);
                            await context.Response.WriteAsync(json);
                            reader.Dispose();
                            return;
                        }

                        if(context.Request.Path == "/command")
                        {
                            var reader = new StreamReader(context.Request.Body);
                            var command = JsonConvert.DeserializeObject<FakeCommand>(reader.ReadToEnd(), jsonSerializerSettings);
                            logger.LogInformation(new EventId(3), null, $"{baseSchemeUrlAndPort}/command called, my state is {n.State.GetType().FullName}");
                            var commandResponse = await n.Accept(command);
                            var json = JsonConvert.SerializeObject(commandResponse);
                            await context.Response.WriteAsync(json);
                            reader.Dispose();
                            return;
                        }
                    }
                    catch(Exception exception)
                    {
                        Console.WriteLine(exception);
                    }
                });
        }

        private void OnShutdown(IApplicationBuilder app)
        {
            var node = (INode)app.ApplicationServices.GetService(typeof(INode));
            node.Stop();
        }
    }
}
