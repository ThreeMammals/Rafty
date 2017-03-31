using System;
using System.Collections.Generic;

namespace Rafty
{
    using System.Threading.Tasks;

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