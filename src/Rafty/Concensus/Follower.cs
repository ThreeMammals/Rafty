using System;

namespace Rafty.Concensus
{
    public sealed class Follower : IState
    {
        private readonly ISendToSelf _sendToSelf;

        public Follower(CurrentState state, ISendToSelf sendToSelf)
        {
            CurrentState = state;
            _sendToSelf = sendToSelf;
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            _sendToSelf.Publish(new BeginElection());
            return new Candidate(CurrentState, _sendToSelf);
        }

        public IState Handle(BeginElection beginElection)
        {
            return this;
        }

        public IState Handle(AppendEntries appendEntries)
        {
            //todo consolidate with request vote
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log);
                return new Follower(nextState, _sendToSelf);
            }
            
            return this;
        }

        public IState Handle(RequestVote requestVote)
        {
            //todo - consolidate with AppendEntries
            if(requestVote.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, requestVote.Term, CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log);
                return new Follower(nextState, _sendToSelf);
            }

            return this;
        }

        public IState Handle(AppendEntriesResponse appendEntries)
        {
             //todo - consolidate with AppendEntries and RequestVOte
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log);
                return new Follower(nextState, _sendToSelf);
            }

            return this;
        }

        public IState Handle(RequestVoteResponse requestVoteResponse)
        {
             //todo - consolidate with AppendEntries and RequestVOte wtc
            if(requestVoteResponse.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, requestVoteResponse.Term, CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log);
                return new Follower(nextState, _sendToSelf);
            }

            return this;
        }
    }
}