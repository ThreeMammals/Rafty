using System.Collections.Generic;
using Rafty.Commands;

namespace Rafty.State
{
    using System.Threading.Tasks;

    public class FakeStateMachine : IStateMachine
    {
        public List<ICommand> Commands;

        public FakeStateMachine()
        {
            Commands = new List<ICommand>();
        }

        public async Task Apply(ICommand command)
        {
            Commands.Add(command);
        }
    }
}