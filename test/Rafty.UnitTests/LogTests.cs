namespace Rafty.UnitTests
{
    using Log;
    using Shouldly;
    using Xunit;

    public class LogTests
    {
        [Fact]
        public void ShouldInitialiseCorrectly()
        {
            var log = new InMemoryLog();
            log.LastLogIndex.ShouldBe(0);
            log.LastLogTerm.ShouldBe(0);
        }

        [Fact]
        public void ShouldApplyLog()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 1));
        }

        [Fact]
        public void ShouldSetLastLogIndex()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 1));
            log.LastLogIndex.ShouldBe(0);
        }

        [Fact]
        public void ShouldSetLastLogTerm()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 1));
            log.LastLogTerm.ShouldBe(1);
        }

        [Fact]
        public void ShouldGetTermAtIndex()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 1));
            log.GetTermAtIndex(0).ShouldBe(1);
        }

        [Fact]
        public void ShouldDeleteConflict()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 0));
            log.DeleteConflicts(new LogEntry("test", typeof(string), 2, 0));
            log.ExposedForTesting.Count.ShouldBe(0);
        }

        [Fact]
        public void ShouldNotDeleteConflict()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 0));
            log.DeleteConflicts(new LogEntry("test", typeof(string), 1, 0));
            log.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact]
        public void ShouldDeleteConflictAndSubsequentLogs()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 0));
            log.Apply(new LogEntry("test", typeof(string), 1, 1));
            log.Apply(new LogEntry("test", typeof(string), 1, 2));
            log.DeleteConflicts(new LogEntry("test", typeof(string), 2, 0));
            log.ExposedForTesting.Count.ShouldBe(0);
        }
    }
}