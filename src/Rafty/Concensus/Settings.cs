namespace Rafty.Concensus
{
    public class Settings
    {
        public Settings(int minTimeout, int maxTimeout)
        {
            MinTimeout = minTimeout;
            MaxTimeout = maxTimeout;
        }

        public int MinTimeout { get; private set; }
        public int MaxTimeout { get; private set; }
    }
}