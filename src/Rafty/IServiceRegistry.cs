using System.Collections.Generic;

namespace Rafty
{
    public interface IServiceRegistry
    {
        void Register(RegisterService registerService);
        List<Service> Get(string name);
    }
}