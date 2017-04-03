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

        public int Term { get; private set; }
        public ICommand Command { get; private set; }
    }
}