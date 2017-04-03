using System;
using Rafty.Commands;

namespace Rafty.Messages
{
    public class SendLeaderCommand : Message
    {
        public SendLeaderCommand(ICommand command, Guid leaderId)
        {
            Command = command;
            LeaderId = leaderId;
        }

        public ICommand Command { get; private set; }
        public Guid LeaderId { get; private set; }
    }
}