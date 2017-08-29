using System;
using System.Threading.Tasks;
using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    public class InMemoryStateMachine : IFiniteStateMachine
    {
        public int ExposedForTesting {get;private set;}

        public void Handle<T>(T command)
        {
            ExposedForTesting++;
        }

        public async Task HandleAsync<T>(T command)
        {
            ExposedForTesting++;
        }
    }
}