namespace Rafty.Infrastructure
{
    public class InMemorySettings : ISettings
    {
        public InMemorySettings(int minTimeout, int maxTimeout, int heartbeatTimeout, int commandTimeout)
        {
            MinTimeout = minTimeout;
            MaxTimeout = maxTimeout;
            HeartbeatTimeout = heartbeatTimeout;
            CommandTimeout = commandTimeout;
        }

        public int MinTimeout { get; private set; }
        public int MaxTimeout { get; private set; }
        public int HeartbeatTimeout { get; private set; }
        public int CommandTimeout { get; private set; }
    }
}