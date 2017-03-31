using System;

namespace Rafty.ServiceDiscovery
{
    public class RemoteServerLocation
    {
        public RemoteServerLocation(string url, Guid id)
        {
            this.Url = url;
        }

        public string Url { get; private set; }
        public Guid Id { get; private set; }
    }
}