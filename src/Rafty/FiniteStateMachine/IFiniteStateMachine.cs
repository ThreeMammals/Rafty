namespace Rafty.FiniteStateMachine
{
    using System.Threading.Tasks;

    public interface IFiniteStateMachine
    {
        void Handle<T>(T command);
        Task HandleAsync<T>(T command);
    }
}