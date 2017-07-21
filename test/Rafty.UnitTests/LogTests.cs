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
    }
}