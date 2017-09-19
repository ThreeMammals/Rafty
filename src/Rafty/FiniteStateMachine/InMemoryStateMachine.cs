using System;
using System.Threading.Tasks;
using Rafty.FiniteStateMachine;

namespace Rafty.FiniteStateMachine
{
    public class InMemoryStateMachine : IFiniteStateMachine
    {
        public int ExposedForTesting {get;private set;}

        public void Handle<T>(T command)
        {
            ExposedForTesting++;
        }
    }
}