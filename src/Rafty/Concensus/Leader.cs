namespace Rafty.Concensus
{
    using System;

    public sealed class Leader : IState
    {
        public Leader(CurrentState currentState)
        {
            CurrentState = currentState;
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            throw new NotImplementedException();
        }

        public IState Handle(BeginElection beginElection)
        {
            throw new NotImplementedException();
        }

        public IState Handle(AppendEntries appendEntries)
        {
            throw new NotImplementedException();
        }
    }
}