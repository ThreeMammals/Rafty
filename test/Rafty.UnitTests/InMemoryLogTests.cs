namespace Rafty.UnitTests
{
    using Log;
    using Shouldly;
    using Xunit;

    public class InMemoryLogTests
    {
        [Fact]
        public void ShouldInitialiseCorrectly()
        {
            var log = new InMemoryLog();
            log.LastLogIndex.ShouldBe(1);
            log.LastLogTerm.ShouldBe(0);
        }

        [Fact]
        public void ShouldApplyLog()
        {
            var log = new InMemoryLog();
            var index = log.Apply(new LogEntry("test", typeof(string), 1));
            index.ShouldBe(1);
        }

        [Fact]
        public void ShouldSetLastLogIndex()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.LastLogIndex.ShouldBe(1);
        }

        [Fact]
        public void ShouldSetLastLogTerm()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.LastLogTerm.ShouldBe(1);
        }

        [Fact]
        public void ShouldGetTermAtIndex()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.GetTermAtIndex(1).ShouldBe(1);
        }

        [Fact]
        public void ShouldDeleteConflict()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.DeleteConflictsFromThisLog(1, new LogEntry("test", typeof(string), 2));
            log.ExposedForTesting.Count.ShouldBe(0);
        }

        [Fact]
        public void ShouldNotDeleteConflict()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.DeleteConflictsFromThisLog(1, new LogEntry("test", typeof(string), 1));
            log.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact]
        public void ShouldDeleteConflictAndSubsequentLogs()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.DeleteConflictsFromThisLog(1, new LogEntry("test", typeof(string), 2));
            log.ExposedForTesting.Count.ShouldBe(0);
        }

        [Fact]
        public void ShouldDeleteConflictAndSubsequentLogsFromMidPoint()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.Apply(new LogEntry("test", typeof(string), 1));
            log.DeleteConflictsFromThisLog(4, new LogEntry("test", typeof(string), 2));
            log.ExposedForTesting.Count.ShouldBe(3);
            log.ExposedForTesting[1].Term.ShouldBe(1);
            log.ExposedForTesting[2].Term.ShouldBe(1);
            log.ExposedForTesting[3].Term.ShouldBe(1);
        }
    }
}