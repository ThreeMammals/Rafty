using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rafty.Infrastructure;
using Rafty.Log;
using Shouldly;
using Xunit;
using System.Threading;
using Rafty.FiniteStateMachine;
using Moq;
using Microsoft.Extensions.Logging;

namespace Rafty.IntegrationTests
{
    public class FileFsmTests : IDisposable
    {
        [Fact]
        public async Task ShouldStoreMultiple()
        {
            var loggerFactory = new Mock<ILoggerFactory>();
            var logger = new Mock<ILogger>();
            loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
            var fsm = new FileFsm(new NodeId("test"), loggerFactory.Object);
            await fsm.Handle(new LogEntry(new FakeCommand("balls"), typeof(FakeCommand), 1));
            await fsm.Handle(new LogEntry(new FakeCommand("bats"), typeof(FakeCommand), 2));
            Thread.Sleep(1000);
            var text = await File.ReadAllTextAsync("test");
            var storedCommands = JsonConvert.DeserializeObject<List<ICommand>>(text, new JsonSerializerSettings() { 
                TypeNameHandling = TypeNameHandling.All
            });
            storedCommands.Count.ShouldBe(2);
        }

        public void Dispose()
        {
            File.Delete("test");
        }
    }
}