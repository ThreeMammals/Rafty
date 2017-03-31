namespace Rafty
{
    public interface IStateMachine
    {
        void Apply(ICommand command);
    }
}