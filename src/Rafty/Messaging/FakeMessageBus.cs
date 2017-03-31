using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rafty.Commands;
using Rafty.Messages;
using Rafty.Responses;

namespace Rafty.Messaging
{
    public class FakeMessageBus : IMessageBus
    {
        public List<IMessage> SendToSelfMessages { get; private set; }

        public FakeMessageBus()
        {
            SendToSelfMessages = new List<IMessage>();
        }

        public void Stop()
        {
            
        }

        public void Publish(SendToSelf message)
        {
            SendToSelfMessages.Add(message);
        }

        public async Task<AppendEntriesResponse> Send(AppendEntries appendEntries)
        {
            return default(AppendEntriesResponse);
        }

        public async Task<RequestVoteResponse> Send(RequestVote requestVote)
        {
            return default(RequestVoteResponse);
        }

        public async Task<SendLeaderCommandResponse> Send(ICommand command, Guid leaderId)
        {
            return default(SendLeaderCommandResponse);
        }
    }
}