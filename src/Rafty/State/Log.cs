using Newtonsoft.Json;
using Rafty.Commands;

namespace Rafty.State
{
    public class Log
    {
        public Log(int term, ICommand command)
        {
            Term = term;
            Command = command;
        }

        [JsonConstructor]
        public Log(int term, FakeCommand command)
        {
            Command = command;
            Term = term;
        }

        public int Term { get; private set; }
        public ICommand Command { get; private set; }
    }
}