namespace Rafty.Concensus
{
    using System;
    using Rafty.FiniteStateMachine;

    public sealed class Leader : IState
    {
        private ISendToSelf _sendToSelf;
        private IFiniteStateMachine _fsm;
        public Leader(CurrentState currentState, ISendToSelf sendToSelf, IFiniteStateMachine fsm)
        {
            _fsm = fsm;
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
            
            CurrentState nextState = CurrentState;

            if(appendEntries.Term >= CurrentState.CurrentTerm)
            {
                //If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)
                var commitIndex = CurrentState.CommitIndex;
                var lastApplied = CurrentState.LastApplied;
                if (appendEntries.LeaderCommitIndex > CurrentState.CommitIndex)
                {
                    //This only works because of the code in the node class that handles the message first (I think..im a bit stupid)
                    var lastNewEntry = CurrentState.Log.LastLogIndex;
                    commitIndex = System.Math.Min(appendEntries.LeaderCommitIndex, lastNewEntry);
                }

                nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, nextState.CurrentTerm, 
                    CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log, commitIndex, lastApplied);
            }

            //todo consolidate with request vote
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, 
                    CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex, CurrentState.LastApplied);
                return new Follower(nextState, _sendToSelf, _fsm);
            }

            return new Leader(nextState, _sendToSelf, _fsm);
        }

        public IState Handle(RequestVote requestVote)
        {
            //todo - consolidate with AppendEntries
            if(requestVote.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, requestVote.Term, CurrentState.VotedFor, 
                    CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex, CurrentState.LastApplied);
                return new Follower(nextState, _sendToSelf, _fsm);
            }

            //leader cannot vote for anyone else...
            return this;
        }

        public IState Handle(AppendEntriesResponse appendEntries)
        {
             //todo - consolidate with AppendEntries and RequestVOte
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, CurrentState.VotedFor, 
                    CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex, CurrentState.LastApplied);
                return new Follower(nextState, _sendToSelf, _fsm);
            }

            return this;
        }

        public IState Handle(RequestVoteResponse requestVoteResponse)
        {
             //todo - consolidate with AppendEntries and RequestVOte wtc
            if(requestVoteResponse.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, requestVoteResponse.Term, CurrentState.VotedFor, 
                    CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex, CurrentState.LastApplied);
                return new Follower(nextState, _sendToSelf, _fsm);
            }

            return this;
        }
    }
}