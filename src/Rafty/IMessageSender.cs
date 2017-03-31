using System;

namespace Rafty
{
    using System.Threading.Tasks;

    public interface IMessageSender
    {
        void Send(SendToSelf message);
        void SetServer(Server server);
        Task<AppendEntriesResponse> Send(AppendEntries appendEntries);
        Task<RequestVoteResponse> Send(RequestVote requestVote);
        Task<SendLeaderCommandResponse> Send(ICommand command, Guid leaderId);
    }
}