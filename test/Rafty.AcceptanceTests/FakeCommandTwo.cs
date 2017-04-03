using System;
using Rafty.Commands;

namespace Rafty.AcceptanceTests
{
    public class FakeCommandTwo : Command
    {
        public FakeCommandTwo()
        {

        }

        public FakeCommandTwo(string description)
        {
            Description = description;

        }

        public string Description { get; set; }
    }
}