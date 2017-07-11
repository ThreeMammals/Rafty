using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        public Candidate(CurrentState currentState) 
        {
            // • On conversion to candidate, start election:
            // • Increment currentTerm
            // • Vote for self
            // • Reset election timer
            // • Send RequestVote RPCs to all other servers
            var nextTerm = currentState.CurrentTerm + 1;
            var nextState = new CurrentState(currentState.Id, currentState.Peers, nextTerm);
            CurrentState = nextState;
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