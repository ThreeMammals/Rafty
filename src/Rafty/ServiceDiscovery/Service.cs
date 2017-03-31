using System;

namespace Rafty.ServiceDiscovery
{
    public class Service
    {
        public Service(string name, Guid id, Uri location)
        {
            Name = name;
            Id = id;
            Location = location;
        }

        public string Name { get; private set; }
        public Guid Id { get; private set; }
        public Uri Location { get; private set; }
    }
}