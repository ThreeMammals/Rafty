namespace Rafty.Concensus
{
    public interface IState
    {
        IState Handle(Timeout timeout);
        CurrentState CurrentState {get;}
    }
}