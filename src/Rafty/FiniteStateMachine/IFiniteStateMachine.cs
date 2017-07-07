namespace Rafty.FiniteStateMachine
{
    public interface IFiniteStateMachine
    {
        void Handle<T>(T command);
        TOut Handle<TIn, TOut>(TIn command);
    }
}
