using System.Collections.Generic;
using System.Linq;

namespace Rafty.ServiceDiscovery
{
    public class InMemoryServiceRegistry : IServiceRegistry
    {
        public InMemoryServiceRegistry()
        {
            Services = new List<Service>();
        }
        public List<Service> Services { get; private set; }

        public void Register(RegisterService registerService)
        {
            Services.Add(new Service(registerService.Name, registerService.Id, registerService.Location));
        }

        public List<Service> Get(string name)
        {
            return Services.Where(x => x.Name == name).ToList();
        }
    }
}