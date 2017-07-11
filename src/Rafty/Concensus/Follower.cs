using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public class Follower : IState
    {
        private TimeoutMessager _bus;

        public Follower(CurrentState state, TimeoutMessager bus)
        {
            CurrentState = state;
            _bus = bus;
        }

        public CurrentState CurrentState {get;private set;}

        public IState Handle(Timeout timeout)
        {
            //begin election.....
            _bus.Publish(new BeginElection());
            return new Candidate(CurrentState);
        }

        public IState Handle(BeginElection beginElection)
        {
            return this;
        }
    }
}