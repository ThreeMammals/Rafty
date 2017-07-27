namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using Concensus;

    internal class FakeNode : INode
    {
        public FakeNode()
        {
            Messages = new List<Message>();
        }

        public List<Message> Messages { get; }

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

        public Response<T> Accept<T>(T command)
        {
            throw new NotImplementedException();
        }
    }
}