using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    using System.Threading.Tasks;

    public interface IState
    {
        CurrentState CurrentState { get; }
        Task<AppendEntriesResponse> Handle(AppendEntries appendEntries);
        Task<RequestVoteResponse> Handle(RequestVote requestVote);
        Task<Response<T>> Accept<T>(T command) where T : ICommand;
        void Stop();
    }
}