using System;

namespace Rafty
{
    using System.Threading.Tasks;

    public interface IMessageBus
    {
        void Publish(SendToSelf message);
        void Stop();
        Task<AppendEntriesResponse> Send(AppendEntries appendEntries);
        Task<RequestVoteResponse> Send(RequestVote requestVote);
        Task<SendLeaderCommandResponse> Send(ICommand command, Guid leaderId);
    }
}