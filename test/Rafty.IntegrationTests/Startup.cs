using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;
using Rafty.Infrastructure;
using Rafty.Log;
using ConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace Rafty.IntegrationTests
{
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
            var settings = new InMemorySettings(1000, 3500, 50, 5000);
            services.AddSingleton<ILog, InMemoryLog>();
            services.AddSingleton<IFiniteStateMachine, FileFsm>();
            services.AddSingleton<ISettings>(settings);
            services.AddSingleton<IPeersProvider, FilePeersProvider>();
            services.AddSingleton<INode, Node>();
            services.Configure<FilePeers>(Configuration);
            services.AddLogging();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            var peersProvider = (IPeersProvider)app.ApplicationServices.GetService(typeof(IPeersProvider));
            var peers = peersProvider.Get();
            var webHostBuilder = (IWebHostBuilder)app.ApplicationServices.GetService(typeof(IWebHostBuilder));
            var baseSchemeUrlAndPort = webHostBuilder.GetSetting(WebHostDefaults.ServerUrlsKey);
            var node = (INode)app.ApplicationServices.GetService(typeof(INode));
            var nodeId = (NodeId)app.ApplicationServices.GetService(typeof(NodeId));
            var logger = loggerFactory.CreateLogger<Startup>();

            node.Start(nodeId.Id);
            app.Run(async context =>
                {
                    var n = (INode)context.RequestServices.GetService(typeof(INode));
                    if(context.Request.Path == "/appendentries")
                    {
                        var reader = new StreamReader(context.Request.Body);
                        var appendEntries = JsonConvert.DeserializeObject<AppendEntries>(reader.ReadToEnd());
                        logger.LogInformation(new EventId(1), null, $"{baseSchemeUrlAndPort}/appendentries called state is {n.State.GetType().FullName}");
                        var appendEntriesResponse = n.Handle(appendEntries);
                        var json = JsonConvert.SerializeObject(appendEntriesResponse);
                        await context.Response.WriteAsync(json);
                        reader.Dispose();
                        return;
                    }

                    if (context.Request.Path == "/requestvote")
                    {
                        var reader = new StreamReader(context.Request.Body);
                        var requestVote = JsonConvert.DeserializeObject<RequestVote>(reader.ReadToEnd());
                         logger.LogInformation(new EventId(2), null, $"{baseSchemeUrlAndPort}/requestvote called state is {n.State.GetType().FullName}");
                        var requestVoteResponse = n.Handle(requestVote);
                        var json = JsonConvert.SerializeObject(requestVoteResponse);
                        await context.Response.WriteAsync(json);
                        reader.Dispose();
                        return;
                    }

                    if(context.Request.Path == "/command")
                    {
                        var reader = new StreamReader(context.Request.Body);
                        var command = JsonConvert.DeserializeObject<AppendEntries>(reader.ReadToEnd());
                        logger.LogInformation(new EventId(3), null, $"{baseSchemeUrlAndPort}/command called state is {n.State.GetType().FullName}");
                        var commandResponse = n.Accept(command);
                        var json = JsonConvert.SerializeObject(commandResponse);
                        await context.Response.WriteAsync(json);
                        reader.Dispose();
                        return;
                    }
                });
        }
    }

    public class NodeId
    {
        public NodeId(Guid id)
        {
            Id = id;
        }

        public Guid Id {get;private set;}
    }

    public class FilePeers
    {
        public FilePeers()
        {
            Peers = new List<FilePeer>();
        }

        public List<FilePeer> Peers {get; set;}
    }

    public class FilePeer
    {
        public string Id { get; set; }
        public string HostAndPort { get; set; }
    }

    public class FilePeersProvider : IPeersProvider
    {
        private readonly IOptions<FilePeers> _options;

        public FilePeersProvider(IOptions<FilePeers> options)
        {
            _options = options;
        }
        public List<IPeer> Get()
        {
            var peers = new List<IPeer>();
            foreach (var item in _options.Value.Peers)
            {
                var httpClient = new HttpClient();
                var httpPeer = new HttpPeer(item.HostAndPort, Guid.Parse(item.Id), httpClient);
                peers.Add(httpPeer);
            }
            return peers;
        }
    }

    public class HttpPeer : IPeer
    {
        private string _hostAndPort;
        private HttpClient _httpClient;
        public HttpPeer(string hostAndPort, Guid id, HttpClient httpClient)
        {
            Id  = id;
            _hostAndPort = hostAndPort;
            _httpClient = httpClient;
        }

        public Guid Id {get; private set;}

        public RequestVoteResponse Request(RequestVote requestVote)
        {
            var json = JsonConvert.SerializeObject(requestVote);
            var content = new StringContent(json);
            var response = _httpClient.PostAsync($"{_hostAndPort}/requestvote", content).GetAwaiter().GetResult();
            if(response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<RequestVoteResponse>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            else
            {
                return new RequestVoteResponse(false, requestVote.Term);
            }
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            try
            {
                var json = JsonConvert.SerializeObject(appendEntries);
                var content = new StringContent(json);
                var response = _httpClient.PostAsync($"{_hostAndPort}/appendEntries", content).GetAwaiter().GetResult();
                if(response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<AppendEntriesResponse>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                }
                else
                {
                    return new AppendEntriesResponse(appendEntries.Term, false);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return new AppendEntriesResponse(appendEntries.Term, false);
            }
        }

        public Response<T> Request<T>(T command)
        {
            var json = JsonConvert.SerializeObject(command);
            var content = new StringContent(json);
            var response = _httpClient.PostAsync($"{_hostAndPort}/command", content).GetAwaiter().GetResult();
            if(response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<Response<T>>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            else 
            {
                return new Response<T>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), command);
            }
        }
    }

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
