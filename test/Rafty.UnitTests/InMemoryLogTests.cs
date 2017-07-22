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
            log.DeleteConflictsFromThisLog(new LogEntry("test", typeof(string), 2, 0));
            log.ExposedForTesting.Count.ShouldBe(0);
        }

        [Fact]
        public void ShouldNotDeleteConflict()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 0));
            log.DeleteConflictsFromThisLog(new LogEntry("test", typeof(string), 1, 0));
            log.ExposedForTesting.Count.ShouldBe(1);
        }

        [Fact]
        public void ShouldDeleteConflictAndSubsequentLogs()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 0));
            log.Apply(new LogEntry("test", typeof(string), 1, 1));
            log.Apply(new LogEntry("test", typeof(string), 1, 2));
            log.DeleteConflictsFromThisLog(new LogEntry("test", typeof(string), 2, 0));
            log.ExposedForTesting.Count.ShouldBe(0);
        }

        [Fact]
        public void ShouldDeleteConflictAndSubsequentLogsFromMidPoint()
        {
            var log = new InMemoryLog();
            log.Apply(new LogEntry("test", typeof(string), 1, 0));
            log.Apply(new LogEntry("test", typeof(string), 1, 1));
            log.Apply(new LogEntry("test", typeof(string), 1, 2));
            log.Apply(new LogEntry("test", typeof(string), 1, 3));
            log.Apply(new LogEntry("test", typeof(string), 1, 4));
            log.DeleteConflictsFromThisLog(new LogEntry("test", typeof(string), 2, 3));
            log.ExposedForTesting.Count.ShouldBe(3);
            log.ExposedForTesting[0].Term.ShouldBe(1);
            log.ExposedForTesting[0].CurrentCommitIndex.ShouldBe(0);
            log.ExposedForTesting[1].Term.ShouldBe(1);
            log.ExposedForTesting[1].CurrentCommitIndex.ShouldBe(1);
            log.ExposedForTesting[2].Term.ShouldBe(1);
            log.ExposedForTesting[2].CurrentCommitIndex.ShouldBe(2);
        }
    }
}