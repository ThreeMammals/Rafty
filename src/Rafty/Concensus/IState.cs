namespace Rafty.Concensus
{
    public interface IState
    {
        IState Handle(Timeout timeout);
        IState Handle(VoteForSelf voteForSelf);
        CurrentState CurrentState {get;}
    }
}