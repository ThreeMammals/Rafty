using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Rafty.Log;
using Xunit.Abstractions;
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
using Rafty.FiniteStateMachine;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Rafty.IntegrationTests
{
    public class Tests : IDisposable
    {
        private readonly List<IWebHost> _builders;
        private readonly List<IWebHostBuilder> _webHostBuilders;
        private readonly List<Thread> _threads;
        private FilePeers _peers;
        private ITestOutputHelper _output;

        public Tests(ITestOutputHelper output)
        {
            _output = output;
            _webHostBuilders = new List<IWebHostBuilder>();
            _builders = new List<IWebHost>();
            _threads = new List<Thread>();
        }

        [Fact]
        public async Task ShouldPersistCommandToFiveServers()
        {
            var peers = new List<FilePeer>
            {
                new FilePeer {HostAndPort = "http://localhost:5000"},

                new FilePeer {HostAndPort = "http://localhost:5001"},

                new FilePeer {HostAndPort = "http://localhost:5002"},

                new FilePeer {HostAndPort = "http://localhost:5003"},

                new FilePeer {HostAndPort = "http://localhost:5004"}
            };
      
            var command = new FakeCommand("WHATS UP DOC?");
            GivenThePeersAre(peers);
            await GivenFiveServersAreRunning();
            await WhenISendACommandIntoTheCluster(command);
            Thread.Sleep(10000);
            await ThenTheCommandIsReplicatedToAllStateMachines(command, 1);
        }

        [Fact]
        public async Task ShouldPersistTwoCommandsToFiveServers()
        {
            var peers = new List<FilePeer>
            {
                new FilePeer {HostAndPort = "http://localhost:5005"},

                new FilePeer {HostAndPort = "http://localhost:5006"},

                new FilePeer {HostAndPort = "http://localhost:5007"},

                new FilePeer {HostAndPort = "http://localhost:5008"},

                new FilePeer {HostAndPort = "http://localhost:5009"}
            };
      
            var command = new FakeCommand("WHATS UP DOC?");
            GivenThePeersAre(peers);            
            await GivenFiveServersAreRunning();
            await WhenISendACommandIntoTheCluster(command);
            await WhenISendACommandIntoTheCluster(command);
            Thread.Sleep(10000);
            await ThenTheCommandIsReplicatedToAllStateMachines(command, 2);
        }

        private void GivenThePeersAre(List<FilePeer> peers)
        {
            FilePeers filePeers = new FilePeers();
            filePeers.Peers.AddRange(peers);
            var json = JsonConvert.SerializeObject(filePeers);
            File.WriteAllText("peers.json", json);
        }

        private void GivenAServerIsRunning(string url)
        {
            IWebHostBuilder webHostBuilder = new WebHostBuilder();
            webHostBuilder.UseUrls(url)
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureServices(x =>
                {
                    x.AddLogging();
                    x.AddSingleton(webHostBuilder);
                    x.AddSingleton(new NodeId(url));
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
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
                        var error = JsonConvert.DeserializeObject<ErrorResponse<FakeCommand>>(content);
                        if (!string.IsNullOrEmpty(error.Error))
                        {
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

            var leaderElectedAndCommandReceived = await WaitFor(40000).Until(async () =>
            {
                var result =  await SendCommand();
                Thread.Sleep(1000);
                return result;
            });
            leaderElectedAndCommandReceived.ShouldBeTrue();
        }

        private async Task ThenTheCommandIsReplicatedToAllStateMachines(FakeCommand fakeCommand, int expectedCommands)
        {
            async Task<bool> CommandCalledOnAllStateMachines()
            {
                try
                {
                    var passed = 0;
                    foreach (var peer in _peers.Peers)
                    {
                        var path = $"{peer.HostAndPort.Replace("/","").Replace(":","")}.db";
                        using(var connection = new SqliteConnection($"Data Source={path};"))
                        {
                            connection.Open();
                            var sql = @"select count(id) from logs";
                            using(var command = new SqliteCommand(sql, connection))
                            {
                                var count = Convert.ToInt32(command.ExecuteScalar());
                                if(count != expectedCommands)
                                {
                                    LogInformation($"{peer.HostAndPort} had {count} logs, expected {expectedCommands}");
                                    continue;
                                }
                            }
                        }
                        
                        var fsmData = await File.ReadAllTextAsync(peer.HostAndPort.Replace("/", "").Replace(":", ""));

                        fsmData.ShouldNotBeNullOrEmpty();

                        var storedCommands = JsonConvert.DeserializeObject<List<ICommand>>(fsmData, new JsonSerializerSettings() { 
                            TypeNameHandling = TypeNameHandling.All
                        });

                        if(storedCommands.Count != expectedCommands)
                        {
                            LogInformation($"{peer.HostAndPort} had {fsmData.Length} length in fsm file and stored {storedCommands.Count} commands");
                            continue;
                        }
                        else
                        {
                            foreach(var command in storedCommands)
                            {
                                var fC = (FakeCommand)command;
                                fC.Value.ShouldBe(fakeCommand.Value);
                            }
                            passed++;
                        }
                    }

                    return passed == 5;
                }
                catch (Exception e)
                {
                    LogException(e);
                    return false;
                }
            }
            
            var commandOnAllStateMachines = await WaitFor(20000).Until(async () =>
            {
                var result = await CommandCalledOnAllStateMachines();
                Thread.Sleep(1000);
                return result;
            });

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
                LogInformation($"Deleting files for {peer.HostAndPort.Replace("/", "").Replace(":", "")}");
                File.Delete(peer.HostAndPort.Replace("/", "").Replace(":", ""));
                File.Delete($"{peer.HostAndPort.Replace("/", "").Replace(":", "")}.db");
            }
        }

        private void LogInformation(string message)
        {
            _output.WriteLine(message);
            Console.WriteLine(message);
            Debug.WriteLine(message);
        }

        private void LogException(Exception e)
        {
            _output.WriteLine($"{e.Message}, {e.StackTrace}");
            Console.WriteLine(e);
            Debug.WriteLine(e);
        }
    }
}
