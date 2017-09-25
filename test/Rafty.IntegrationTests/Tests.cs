using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;

namespace Rafty.IntegrationTests
{
    public class Tests : IDisposable
    {
        private List<IWebHost> _builders;
        private List<IWebHostBuilder> _webHostBuilders;

        public Tests()
        {
            _webHostBuilders = new List<IWebHostBuilder>();
            _builders = new List<IWebHost>();
        }
        public void Dispose()
        {
            foreach(var builder in _builders)
            {
                builder.Dispose();
            }
        }

        [Fact]
        public void ShouldDoSomething()
        {
            var bytes = File.ReadAllText("peers.json");
            var peers = JsonConvert.DeserializeObject<FilePeers>(bytes);

            foreach(var peer in peers.Peers)
            {
                GivenAServerIsRunning(peer.HostAndPort, peer.Id);
            }
            
            var stopWatch = Stopwatch.StartNew();
            while(stopWatch.ElapsedMilliseconds < 10000)
            {

            }
            //now try sending a command and see if it gets replicated?
        }

        private void GivenAServerIsRunning(string url, string id)
        {
            var guid = Guid.Parse(id);

            IWebHostBuilder webHostBuilder = new WebHostBuilder();
            webHostBuilder.UseUrls(url)
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureServices(x => {
                    x.AddSingleton(webHostBuilder);
                    x.AddSingleton(new NodeId(guid));
                })
                .UseStartup<Startup>();

              var builder = webHostBuilder.Build();
              builder.Start();

              _webHostBuilders.Add(webHostBuilder);
              _builders.Add(builder);
        }
    }
}
