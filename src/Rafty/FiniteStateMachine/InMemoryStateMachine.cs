using System;
using System.Threading.Tasks;
using Rafty.FiniteStateMachine;
using Rafty.Log;

namespace Rafty.FiniteStateMachine
{
    public class InMemoryStateMachine : IFiniteStateMachine
    {
        public int ExposedForTesting {get;private set;}

        public void Handle(LogEntry log)
        {
            ExposedForTesting++;
        }
    }
}