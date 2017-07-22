namespace Rafty.Concensus
{
    using System;

    public sealed class Leader : IState
    {
        private ISendToSelf _sendToSelf;
        public Leader(CurrentState currentState, ISendToSelf sendToSelf)
        {
            CurrentState = currentState;
            _sendToSelf = sendToSelf;
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            throw new NotImplementedException();
        }

        public IState Handle(BeginElection beginElection)
        {
            throw new NotImplementedException();
        }

        public IState Handle(AppendEntries appendEntries)
        {
            //todo consolidate with request vote
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                //If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)
                long commitIndex = CurrentState.CommitIndex;

                if (appendEntries.LeaderCommitIndex > CurrentState.CommitIndex)
                {
                    //This only works because of the code in the node class that handles the message first (I think..im a bit stupid)
                    var indexOfLastNewEntry = appendEntries.PreviousLogIndex + 1;
                    commitIndex = System.Math.Min(appendEntries.LeaderCommitIndex, indexOfLastNewEntry);
                }

                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, 
                    CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log, commitIndex);
                return new Follower(nextState, _sendToSelf);
            }
   
            return this;
        }

        public IState Handle(RequestVote requestVote)
        {
            //todo - consolidate with AppendEntries
            if(requestVote.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, requestVote.Term, CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex);
                return new Follower(nextState, _sendToSelf);
            }

            return this;
        }

        public IState Handle(AppendEntriesResponse appendEntries)
        {
             //todo - consolidate with AppendEntries and RequestVOte
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex);
                return new Follower(nextState, _sendToSelf);
            }

            return this;
        }

        public IState Handle(RequestVoteResponse requestVoteResponse)
        {
             //todo - consolidate with AppendEntries and RequestVOte wtc
            if(requestVoteResponse.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, requestVoteResponse.Term, CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex);
                return new Follower(nextState, _sendToSelf);
            }

            return this;
        }
    }
}