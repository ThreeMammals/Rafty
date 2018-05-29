namespace Rafty.UnitTests
{
    using System;
    using System.Threading.Tasks;
    using Log;
    using Shouldly;
    using Xunit;

    public class InMemoryLogTests
    {
        [Fact]
        public void ShouldInitialiseCorrectly()
        {
            var log = new InMemoryLog();
            log.LastLogIndex().Result.ShouldBe(1);
            log.LastLogTerm().Result.ShouldBe(0);
        }

        [Fact]
        public async Task ShouldApplyLog()
        {
            var log = new InMemoryLog();
            var index = await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            index.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldSetLastLogIndex()
        {
            var log = new InMemoryLog();
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            log.LastLogIndex().Result.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldSetLastLogTerm()
        {
            var log = new InMemoryLog();
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            log.LastLogTerm().Result.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldGetTermAtIndex()
        {
            var log = new InMemoryLog();
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            log.GetTermAtIndex(1).Result.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldDeleteConflict()
        {
            var log = new InMemoryLog();
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            log.ExposedForTesting.Count.ShouldBe(0);
        }

        [Fact]
        public async Task ShouldNotDeleteConflict()
        {
            var log = new InMemoryLog();
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 1));
            log.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldDeleteConflictAndSubsequentLogs()
        {
            var log = new InMemoryLog();
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.DeleteConflictsFromThisLog(1, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            log.ExposedForTesting.Count.ShouldBe(0);
        }

        [Fact]
        public async Task ShouldDeleteConflictAndSubsequentLogsFromMidPoint()
        {
            var log = new InMemoryLog();
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.DeleteConflictsFromThisLog(4, new LogEntry(new FakeCommand("test"), typeof(string), 2));
            log.ExposedForTesting.Count.ShouldBe(3);
            log.ExposedForTesting[1].Term.ShouldBe(1);
            log.ExposedForTesting[2].Term.ShouldBe(1);
            log.ExposedForTesting[3].Term.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldRemoveFromLog()
        {
            var log = new InMemoryLog();
            var index = await log.Apply(new LogEntry(new FakeCommand("test"), typeof(string), 1));
            await log.Remove(index);
            log.Count().Result.ShouldBe(0);
        }

        [Fact]
        public async Task ShouldBeDuplicate()
        {
            var log = new InMemoryLog();
            var entry = new LogEntry(new FakeCommand("test"), typeof(string), 1);
            var index = await log.Apply(entry);
            var result = await log.IsDuplicate(index, entry);
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task ShouldNotBeDuplicate()
        {
            var log = new InMemoryLog();
            var entry = new LogEntry(new FakeCommand("test"), typeof(string), 1);
            var index = await log.Apply(entry);
            var newEntry = new LogEntry(new FakeCommand("test"), typeof(string), 2);
            var result = await log.IsDuplicate(index, newEntry);
            result.ShouldBeFalse();
        }
    }
}