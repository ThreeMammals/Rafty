using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public class Follower : IState
    {
        public Follower(CurrentState state)
        {
            CurrentState = state;
        }

        public CurrentState CurrentState {get;private set;}

        public IState Handle(Timeout timeout)
        {
            return new Candidate(CurrentState);
        }
    }
}