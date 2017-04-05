using Rafty.Commands;

namespace Rafty.State
{
    using System.Threading.Tasks;

    public interface IStateMachine
    {
        Task Apply(ICommand command);
    }
}