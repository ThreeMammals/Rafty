using System;
using Rafty.Commands;

namespace Rafty.AcceptanceTests
{
    public class FakeCommand : Command
    {
        public FakeCommand()
        {
            
        }

        public FakeCommand(Guid id)
        {
            Id = id;

        }
        public Guid Id { get; set; }
    }
}