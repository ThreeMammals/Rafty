using System;

namespace Rafty
{
    public class FakeCommand : Command
    {
        public FakeCommand(Guid id)
        {
            this.Id = id;

        }
        public Guid Id { get; set; }
    }
}