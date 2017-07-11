using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public sealed class Leader : IState
    {
        public Leader(CurrentState currentState) 
        {
            CurrentState = currentState;
        }

        public CurrentState CurrentState {get;private set;}

        public IState Handle(Timeout timeout)
        {
            throw new NotImplementedException();
        }
        
        public IState Handle(VoteForSelf voteForSelf)
        {
            throw new NotImplementedException();
        }
    }
}