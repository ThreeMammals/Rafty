using System.Collections.Generic;
using Rafty.Commands;

namespace Rafty.State
{
    public class FakeStateMachine : IStateMachine
    {
        public List<ICommand> Commands;

        public FakeStateMachine()
        {
            Commands = new List<ICommand>();
        }

        public void Apply(ICommand command)
        {
            Commands.Add(command);
        }
    }
}