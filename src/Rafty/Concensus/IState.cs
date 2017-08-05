namespace Rafty.Concensus
{
    public interface IState
    {
        CurrentState CurrentState { get; }
        AppendEntriesResponse Handle(AppendEntries appendEntries);
        RequestVoteResponse Handle(RequestVote requestVote);
        Response<T> Accept<T>(T command);
    }
}