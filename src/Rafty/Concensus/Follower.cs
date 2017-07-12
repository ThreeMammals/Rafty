using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public class Follower : IState
    {
        private readonly ISendToSelf _sendToSelf;

        public Follower(CurrentState state, ISendToSelf sendToSelf)
        {
            CurrentState = state;
            _sendToSelf = sendToSelf;
        }

        public CurrentState CurrentState {get;private set;}

        public IState Handle(Timeout timeout)
        {
            //begin election.....
            _sendToSelf.Publish(new BeginElection());
            return new Candidate(CurrentState);
        }

        public IState Handle(BeginElection beginElection)
        {
            return this;
        }
    }
}