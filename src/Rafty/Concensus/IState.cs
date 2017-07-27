namespace Rafty.Concensus
{
    public interface IState
    {
        CurrentState CurrentState { get; }
        IState Handle(Timeout timeout);
        IState Handle(BeginElection beginElection);
        StateAndResponse Handle(AppendEntries appendEntries);
        IState Handle(AppendEntriesResponse appendEntriesResponse);
        IState Handle(RequestVote requestVote);
        IState Handle(RequestVoteResponse requestVoteResponse);
        void Handle<T>(T command);
    }

    public class StateAndResponse
    {
        public StateAndResponse(IState state, AppendEntriesResponse response)
        {
            State = state;
            Response = response;
        }
        public IState State {get;private set;}
        public AppendEntriesResponse Response {get; private set;}
    }
}