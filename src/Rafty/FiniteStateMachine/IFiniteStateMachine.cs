namespace Rafty.FiniteStateMachine
{
    using System.Threading.Tasks;

    public interface IFiniteStateMachine
    {
        /// <summary>
        //This will apply the given command to the state machine.
        /// </summary>
        void Handle<T>(T command);
    }
}