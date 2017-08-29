using System;
using System.Collections.Generic;
using System.Text;

namespace Rafty.Concensus
{
    public class SettingsBuilder
    {
        private int _heartbeatTimeout = 50;
        private int _maxTimeout = 350;
        private int _minTimeout = 100;

        public SettingsBuilder WithMinTimeout(int value)
        {
            _minTimeout = value;
            return this;
        }

        public SettingsBuilder WithMaxTimeout(int value)
        {
            _maxTimeout = value;
            return this;
        }

        public SettingsBuilder WithHeartbeatTimeout(int value)
        {
            _heartbeatTimeout = value;
            return this;
        }

        public Settings Build()
        {
            return new Settings(_minTimeout, _maxTimeout, _heartbeatTimeout);
        }
    }
}
