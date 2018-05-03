namespace Rafty.UnitTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Log;
    using Rafty.Infrastructure;
    using Rafty.IntegrationTests;
    using Shouldly;
    using Xunit;

    public class SqlLiteLogTests : IDisposable
    {
        private SqlLiteLog _log;
        private string _id;

        public SqlLiteLogTests()
        {
            _id = Guid.NewGuid().ToString();
            _log = new SqlLiteLog(new NodeId(_id));
        }

        [Fact]
        public void ShouldInitialiseCorrectly()
        {
            var path = Guid.NewGuid().ToString();
            _log.LastLogIndex().Result.ShouldBe(1);
            _log.LastLogTerm().Result.ShouldBe(0);
        }

        [Fact]
        public async Task ShouldApplyLog()
        {
            var index = await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            index.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldSetLastLogIndex()
        {
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.LastLogIndex().Result.ShouldBe(2);
        }

        [Fact]
        public async Task ShouldSetLastLogTerm()
        {
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 2));
            _log.LastLogTerm().Result.ShouldBe(2);
        }

        [Fact]
        public async Task ShouldGetTermAtIndex()
        {
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.GetTermAtIndex(1).Result.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldDeleteConflict()
        {
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            _log.Count().Result.ShouldBe(0);
        }

        [Fact]
        public async Task ShouldNotDeleteConflict()
        {
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Count().Result.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldDeleteConflictAndSubsequentLogs()
        {
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            _log.Count().Result.ShouldBe(0);
        }

        [Fact]
        public async Task ShouldDeleteConflictAndSubsequentLogsFromMidPoint()
        {
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.DeleteConflictsFromThisLog(4, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            _log.Count().Result.ShouldBe(3);
            _log.Get(1).Result.Term.ShouldBe(1);
            _log.Get(2).Result.Term.ShouldBe(1);
            _log.Get(3).Result.Term.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldGetFrom()
        {
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            var logs = await _log.GetFrom(3);
            logs.Count.ShouldBe(3);
        }

        [Fact]
        public async Task ShouldRemoveFromLog()
        {
            var index = await _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await _log.Remove(index);
            _log.Count().Result.ShouldBe(0);
        }
        public void Dispose()
        {
            File.Delete($"{_id.ToString()}.db");
        }
    }
}