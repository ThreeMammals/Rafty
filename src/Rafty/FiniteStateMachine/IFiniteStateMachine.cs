using Rafty.Log;

namespace Rafty.FiniteStateMachine
{
    using System.Threading.Tasks;

    public interface IFiniteStateMachine
    {
        /// <summary>
        /// This will apply the given log to the state machine.
        /// </summary>
        Task Handle(LogEntry log);
    }
}