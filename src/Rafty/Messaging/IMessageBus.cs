using System;
using System.Threading.Tasks;
using Rafty.Commands;
using Rafty.Messages;
using Rafty.Responses;

namespace Rafty.Messaging
{
    public interface IMessageBus
    {
        void Publish(SendToSelf message);
        void Dispose();
        Task<AppendEntriesResponse> Send(AppendEntries appendEntries);
        Task<RequestVoteResponse> Send(RequestVote requestVote);
        Task<SendLeaderCommandResponse> Send(ICommand command, Guid leaderId);
    }
}