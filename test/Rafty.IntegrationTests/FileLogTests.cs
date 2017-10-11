namespace Rafty.UnitTests
{
    using System;
    using System.IO;
    using Log;
    using Rafty.Infrastructure;
    using Rafty.IntegrationTests;
    using Shouldly;
    using Xunit;

    public class FileLogTests : IDisposable
    {
        private SqlLiteLog _log;
        private Guid _id;

        public FileLogTests()
        {
            _id = Guid.NewGuid();
            _log = new SqlLiteLog(new NodeId(_id));
        }

        [Fact]
        public void ShouldInitialiseCorrectly()
        {
            var path = Guid.NewGuid().ToString();
            _log.LastLogIndex.ShouldBe(1);
            _log.LastLogTerm.ShouldBe(0);
        }

        [Fact]
        public void ShouldApplyLog()
        {
            var index = _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            index.ShouldBe(1);
        }

        [Fact]
        public void ShouldSetLastLogIndex()
        {
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.LastLogIndex.ShouldBe(2);
        }

        [Fact]
        public void ShouldSetLastLogTerm()
        {
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 2));
            _log.LastLogTerm.ShouldBe(2);
        }

        [Fact]
        public void ShouldGetTermAtIndex()
        {
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.GetTermAtIndex(1).ShouldBe(1);
        }

        [Fact]
        public void ShouldDeleteConflict()
        {
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            _log.Count.ShouldBe(0);
        }

        [Fact]
        public void ShouldNotDeleteConflict()
        {
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Count.ShouldBe(1);
        }

        [Fact]
        public void ShouldDeleteConflictAndSubsequentLogs()
        {
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            _log.Count.ShouldBe(0);
        }

        [Fact]
        public void ShouldDeleteConflictAndSubsequentLogsFromMidPoint()
        {
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.DeleteConflictsFromThisLog(4, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            _log.Count.ShouldBe(3);
            _log.Get(1).Term.ShouldBe(1);
            _log.Get(2).Term.ShouldBe(1);
            _log.Get(3).Term.ShouldBe(1);
        }

        [Fact]
        public void ShouldGetFrom()
        {
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            var logs = _log.GetFrom(3);
            logs.Count.ShouldBe(3);
        }

        [Fact]
        public void ShouldRemoveFromLog()
        {
            var index = _log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            _log.Remove(index);
            _log.Count.ShouldBe(0);
        }
        public void Dispose()
        {
            File.Delete($"{_id.ToString()}.db");
        }
    }
}