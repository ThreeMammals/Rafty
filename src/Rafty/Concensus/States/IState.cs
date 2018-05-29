namespace Rafty.Concensus.States
{
    using System.Threading.Tasks;
    using FiniteStateMachine;
    using Infrastructure;
    using Messages;

    public interface IState
    {
        CurrentState CurrentState { get; }
        Task<AppendEntriesResponse> Handle(AppendEntries appendEntries);
        Task<RequestVoteResponse> Handle(RequestVote requestVote);
        Task<Response<T>> Accept<T>(T command) where T : ICommand;
        void Stop();
    }
}