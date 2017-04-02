using System;

namespace Rafty.Commands
{
    public class FakeCommand : Command
    {
        public FakeCommand(Guid id)
        {
            Id = id;

        }
        public Guid Id { get; set; }
    }
}