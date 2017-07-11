using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        public Candidate(CurrentState stateValues) 
        {
            var nextTerm = stateValues.CurrentTerm + 1;
            var nextState = new CurrentState(stateValues.Id, stateValues.Peers, nextTerm);
            CurrentState = nextState;
        }

        public CurrentState CurrentState {get;private set;}

        public IState Handle(Timeout timeout)
        {
            throw new NotImplementedException();
        }

        IState IState.Handle(Timeout timeout)
        {
            throw new NotImplementedException();
        }
    }
}