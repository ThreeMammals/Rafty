using System.Collections.Generic;

namespace Rafty.ServiceDiscovery
{
    public interface IServiceRegistry
    {
        void Register(RegisterService registerService);
        List<Service> Get(string name);
    }
}