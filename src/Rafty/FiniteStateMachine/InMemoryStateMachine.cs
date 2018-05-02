using System.Threading.Tasks;
using Rafty.Log;

namespace Rafty.FiniteStateMachine
{
    public class InMemoryStateMachine : IFiniteStateMachine
    {
        public int HandledLogEntries {get;private set;}

        public Task Handle(LogEntry log)
        {
            HandledLogEntries++;
            return Task.CompletedTask;
        }
    }
}