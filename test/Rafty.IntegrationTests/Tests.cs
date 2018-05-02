using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rafty.Concensus;
using Rafty.Infrastructure;
using Shouldly;
using Xunit;
using static Rafty.Infrastructure.Wait;

namespace Rafty.IntegrationTests
{
    using System.Threading.Tasks;

    public class Tests : IDisposable
    {
        private readonly List<IWebHost> _builders;
        private readonly List<IWebHostBuilder> _webHostBuilders;
        private readonly List<Thread> _threads;
        private FilePeers _peers;

        public Tests()
        {
            _webHostBuilders = new List<IWebHostBuilder>();
            _builders = new List<IWebHost>();
            _threads = new List<Thread>();
        }

        [Fact]
        public async Task ShouldPersistCommandToFiveServers()
        {
            var command = new FakeCommand("WHATS UP DOC?");
            await GivenFiveServersAreRunning();
            await WhenISendACommandIntoTheCluster(command);
            await ThenTheCommandIsReplicatedToAllStateMachines(command);
        }

        private void GivenAServerIsRunning(string url)
        {
            IWebHostBuilder webHostBuilder = new WebHostBuilder();
            webHostBuilder.UseUrls(url)
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureServices(x =>
                {
                    x.AddSingleton(webHostBuilder);
                    x.AddSingleton(new NodeId(url));
                })
                .UseStartup<Startup>();

            var builder = webHostBuilder.Build();
            builder.Start();

            _webHostBuilders.Add(webHostBuilder);
            _builders.Add(builder);
        }

        private async Task GivenFiveServersAreRunning()
        {
            var bytes = await File.ReadAllTextAsync("peers.json");
            _peers = JsonConvert.DeserializeObject<FilePeers>(bytes);

            foreach (var peer in _peers.Peers)
            {
                var thread = new Thread(() => GivenAServerIsRunning(peer.HostAndPort));
                thread.Start();
                _threads.Add(thread);
            }
        }

        private async Task WhenISendACommandIntoTheCluster(FakeCommand command)
        {
            async Task<bool> SendCommand()
            {
                try
                {
                    var p = _peers.Peers.First();
                    var json = JsonConvert.SerializeObject(command);
                    var httpContent = new StringContent(json);
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.PostAsync($"{p.HostAndPort}/command", httpContent);
                        response.EnsureSuccessStatusCode();
                        var content = await response.Content.ReadAsStringAsync();
                        //Console.WriteLine(content);
                        var error = JsonConvert.DeserializeObject<ErrorResponse<FakeCommand>>(content);
                        if (!string.IsNullOrEmpty(error.Error))
                        {
                            //Console.WriteLine(error.Error);
                            return false;
                        }
                        var ok = JsonConvert.DeserializeObject<OkResponse<FakeCommand>>(content);
                        ok.Command.Value.ShouldBe(command.Value);
                        return true;
                    }
                }
                catch(Exception e)
                {
                    return false;
                }
            }

            var leaderElectedAndCommandReceived = await WaitFor(20000).Until(async () => await SendCommand());
            leaderElectedAndCommandReceived.ShouldBeTrue();
        }

        private async Task ThenTheCommandIsReplicatedToAllStateMachines(FakeCommand command)
        {
            async Task<bool> CommandCalledOnAllStateMachines()
            {
                try
                {
                    var passed = 0;
                    foreach (var peer in _peers.Peers)
                    {
                        var fsmData = await File.ReadAllTextAsync(peer.HostAndPort.Replace("/", "").Replace(":", ""));
                        fsmData.ShouldNotBeNullOrEmpty();
                        var fakeCommand = JsonConvert.DeserializeObject<FakeCommand>(fsmData);
                        fakeCommand.Value.ShouldBe(command.Value);
                        passed++;
                    }

                    return passed == 5;
                }
                catch (Exception e)
                {
                    return false;
                }
            }

            var commandOnAllStateMachines = await WaitFor(20000).Until(async () => await CommandCalledOnAllStateMachines());
            commandOnAllStateMachines.ShouldBeTrue();   
        }

        public void Dispose()
        {
            foreach (var builder in _builders)
            {
                builder?.Dispose();
            }

            foreach (var peer in _peers.Peers)
            {
                File.Delete(peer.HostAndPort.Replace("/", "").Replace(":", ""));
                File.Delete($"{peer.HostAndPort.Replace("/", "").Replace(":", "")}.db");
            }
        }
    }
}
