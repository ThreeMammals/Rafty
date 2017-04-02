using System;
using Rafty.Messages;

namespace Rafty.Commands
{
    public abstract class Command : ICommand, IMessage
    {
        protected Command()
        {
            MessageId = Guid.NewGuid();
        }

        public Guid MessageId { get; }
    }

    public interface ICommand
    {

    }
}