using Rafty.FiniteStateMachine;

namespace Rafty.UnitTests
{
    public class FakeCommand : ICommand
    {
        public FakeCommand()
        {
            Data = "asdf";
        }

        public FakeCommand(string data)
        {
            Data = data;
        }
        public string Data { get; private set; }
    }
}