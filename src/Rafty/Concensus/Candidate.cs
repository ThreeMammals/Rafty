using System;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        private readonly ISendToSelf _sendToSelf;
        private int _votesThisElection;

        public Candidate(CurrentState currentState, ISendToSelf sendToSelf)
        {
            _sendToSelf = sendToSelf;
            // • On conversion to candidate, start election:
            // • Increment currentTerm
            var nextTerm = currentState.CurrentTerm + 1;
            // • Vote for self
            _votesThisElection++;
            var votedFor = currentState.Id;
            var nextState = new CurrentState(currentState.Id, currentState.Peers, nextTerm, votedFor,
                currentState.Timeout, currentState.Log);
            CurrentState = nextState;
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            return new Candidate(CurrentState, _sendToSelf);
        }

        public IState Handle(BeginElection beginElection)
        {
            // • On conversion to candidate, start election:
            // • Reset election timer
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
            // • Send RequestVote RPCs to all other servers
            CurrentState.Peers.ForEach(peer =>
            {
                var requestVoteResponse = peer.Request(new RequestVote(CurrentState.CurrentTerm, CurrentState.Id, CurrentState.Log.LastLogIndex, CurrentState.Log.LastLogTerm));

                if (requestVoteResponse.VoteGranted)
                {
                    _votesThisElection++;
                }
            });

            //If votes received from majority of servers: become leader
            if (_votesThisElection >= (CurrentState.Peers.Count + 1) / 2 + 1)
            {
                return new Leader(CurrentState);
            }

            return new Follower(CurrentState, _sendToSelf);
        }

        public IState Handle(AppendEntries appendEntries)
        {
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                var newState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log);
                return new Follower(newState, _sendToSelf);
            }

            return this;
        }

        public IState Handle(RequestVote requestVote)
        {
            throw new NotImplementedException();
        }

        public IState Handle(AppendEntriesResponse appendEntries)
        {
            throw new NotImplementedException();
        }

        public IState Handle(RequestVoteResponse requestVoteResponse)
        {
            throw new NotImplementedException();
        }
    }
}