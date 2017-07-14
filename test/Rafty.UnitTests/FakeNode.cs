using System;
using System.Collections.Generic;
using Rafty.Concensus;

namespace Rafty.UnitTests
{
    internal class FakeNode : INode
    {
        public FakeNode()
        {
            Messages = new List<Message>();
        }

        public List<Message> Messages { get; private set; }

        public IState State { get; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            throw new NotImplementedException();
        }

        public void Handle(Message message)
        {
            Messages.Add(message);
        }
    }
}
