using Rafty.Log;

namespace Rafty.FiniteStateMachine
{
    public class InMemoryStateMachine : IFiniteStateMachine
    {
        public int HandledLogEntries {get;private set;}

        public void Handle(LogEntry log)
        {
            HandledLogEntries++;
        }
    }
}