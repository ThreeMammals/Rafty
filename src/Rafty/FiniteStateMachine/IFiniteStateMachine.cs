using System.Threading.Tasks;

namespace Rafty.FiniteStateMachine
{
    public interface IFiniteStateMachine
    {
        void Handle<T>(T command);
        TOut Handle<TIn, TOut>(TIn command);
        Task HandleAsync<T>(T command);
        Task<TOut> HandleAsync<TIn, TOut>(TIn command);
    }
}
