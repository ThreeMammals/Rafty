namespace Rafty.Infrastructure
{
    public class InMemorySettingsBuilder
    {
        private int _heartbeatTimeout = 50;
        private int _maxTimeout = 350;
        private int _minTimeout = 100;
        private int _commandTimeout = 5000;

        public InMemorySettingsBuilder WithMinTimeout(int value)
        {
            _minTimeout = value;
            return this;
        }

        public InMemorySettingsBuilder WithMaxTimeout(int value)
        {
            _maxTimeout = value;
            return this;
        }

        public InMemorySettingsBuilder WithHeartbeatTimeout(int value)
        {
            _heartbeatTimeout = value;
            return this;
        }
        
        public InMemorySettingsBuilder WithCommandTimeout(int value)
        {
            _commandTimeout = value;
            return this;
        }

        public InMemorySettings Build()
        {
            return new InMemorySettings(_minTimeout, _maxTimeout, _heartbeatTimeout, _commandTimeout);
        }
    }
}
