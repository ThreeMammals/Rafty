namespace Rafty.Concensus
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class TaskAndCancellationToken
    {
        public TaskAndCancellationToken(Task task, CancellationTokenSource cancellationTokenSource)
        {
            Task = task;
            CancellationTokenSource = cancellationTokenSource;
        }

        public Task Task { get; private set; }
        public CancellationTokenSource CancellationTokenSource { get; private set; }
    }
}