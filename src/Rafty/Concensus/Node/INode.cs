using System;
using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    using System.Threading.Tasks;

    public interface INode
    {
        IState State { get; }
        void BecomeLeader(CurrentState state);
        void BecomeFollower(CurrentState state);
        void BecomeCandidate(CurrentState state);
        Task<AppendEntriesResponse> Handle(AppendEntries appendEntries);
        Task<RequestVoteResponse> Handle(RequestVote requestVote);
        void Start(string id);
        void Stop();
        Task<Response<T>> Accept<T>(T command) where T : ICommand;
    }
}