using Rafty.Commands;

namespace Rafty.State
{
    public interface IStateMachine
    {
        void Apply(ICommand command);
    }
}