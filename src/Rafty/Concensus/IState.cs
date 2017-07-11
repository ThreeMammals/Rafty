namespace Rafty.Concensus
{
    public interface IState
    {
        IState Handle(Timeout timeout);
        IState Handle(BeginElection beginElection);
        CurrentState CurrentState {get;}
    }
}