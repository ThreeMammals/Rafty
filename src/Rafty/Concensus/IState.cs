namespace Rafty.Concensus
{
    public interface IState
    {
        CurrentState CurrentState { get; }
        IState Handle(Timeout timeout);
        IState Handle(BeginElection beginElection);
        IState Handle(AppendEntries appendEntries);
        IState Handle(AppendEntriesResponse appendEntriesResponse);
        IState Handle(RequestVote requestVote);
        IState Handle(RequestVoteResponse requestVoteResponse);
        Response<T> Accept<T>(T command);
    }
}