using System;
using System.Threading.Tasks;
using Rafty.Commands;
using Rafty.Messages;
using Rafty.Raft;
using Rafty.Responses;

namespace Rafty.Messaging
{
    public interface IMessageSender
    {
        void Send(SendToSelf message);
        void SetServer(Server server);
        Task<AppendEntriesResponse> Send(AppendEntries appendEntries);
        Task<RequestVoteResponse> Send(RequestVote requestVote);
        Task<SendLeaderCommandResponse> Send(ICommand command, Guid leaderId);
        void Dispose();
    }
}