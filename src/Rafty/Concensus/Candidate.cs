using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        private int _votesThisElection;

        public Candidate(CurrentState currentState) 
        {
            // • On conversion to candidate, start election:
            // • Increment currentTerm
            var nextTerm = currentState.CurrentTerm + 1;
            // • Vote for self
            _votesThisElection++;
            var votedFor = currentState.Id;
            var nextState = new CurrentState(currentState.Id, currentState.Peers, nextTerm, votedFor);
            CurrentState = nextState;
        }

        public CurrentState CurrentState {get;private set;}

        public IState Handle(Timeout timeout)
        {
            return this;
        }

        public IState Handle(BeginElection beginElection)
        {
            // • On conversion to candidate, start election:
            // • Reset election timer
            // • Send RequestVote RPCs to all other servers
            return this;
        }
    }
}