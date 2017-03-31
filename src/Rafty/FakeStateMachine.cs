using System.Collections.Generic;

namespace Rafty
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