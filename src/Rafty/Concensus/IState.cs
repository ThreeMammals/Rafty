namespace Rafty.Concensus
{
    public interface IState
    {
        CurrentState CurrentState { get; }
        IState Handle(Timeout timeout);
        IState Handle(BeginElection beginElection);
        IState Handle(AppendEntries appendEntries);
        IState Handle(RequestVote requestVote);
    }
}