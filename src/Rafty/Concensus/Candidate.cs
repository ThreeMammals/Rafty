using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        public Candidate(CurrentState stateValues) 
        {
            CurrentState = stateValues;
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