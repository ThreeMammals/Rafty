namespace Rafty.Concensus
{
    public class Settings : ISettings
    {
        public Settings(int minTimeout, int maxTimeout, int heartbeatTimeout)
        {
            MinTimeout = minTimeout;
            MaxTimeout = maxTimeout;
            HeartbeatTimeout = heartbeatTimeout;
        }

        public int MinTimeout { get; private set; }
        public int MaxTimeout { get; private set; }
        public int HeartbeatTimeout { get; private set; }
    }

    public interface ISettings
    {
        int MinTimeout { get; }
        int MaxTimeout { get; }
        int HeartbeatTimeout { get; }
    }
}