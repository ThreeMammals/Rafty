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
        Response<T> Handle<T>(T command);
    }

    public class Response<T>
    {
        public Response(bool success, T command)
        {
            Success = success;
            Command = command;
        }

        public bool Success {get;private set;}
        public T Command {get;private set;}
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