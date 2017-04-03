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

    public class FooCommand : Command
    {
        public FooCommand()
        {
        }

        public FooCommand(string description)
        {
            Description = description;
        }

        public string Description {get;set;}
    }
}