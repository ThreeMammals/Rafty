using System;
using System.Linq;

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
            throw new Exception("Follower cannot begin an election?");
        }

        public IState Handle(AppendEntries appendEntries)
        {
            CurrentState nextState = CurrentState;
            //todo consolidate with request vote
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, 
                    CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex, CurrentState.LastApplied);
            }

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
            return new Follower(nextState, _sendToSelf);
        }

        public IState Handle(RequestVote requestVote)
        {
            var term = CurrentState.CurrentTerm;

            //todo - consolidate with AppendEntries
            if(requestVote.Term > CurrentState.CurrentTerm)
            {
                term = requestVote.Term;
            }

            // update voted for....
            var currentState = new CurrentState(CurrentState.Id, CurrentState.Peers, term, requestVote.CandidateId, CurrentState.Timeout, 
                CurrentState.Log, CurrentState.CommitIndex, CurrentState.LastApplied);
            return new Follower(currentState, _sendToSelf);
        }

        public IState Handle(AppendEntriesResponse appendEntries)
        {
             //todo - consolidate with AppendEntries and RequestVOte
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntries.Term, CurrentState.VotedFor, 
                    CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex, CurrentState.LastApplied);
                return new Follower(nextState, _sendToSelf);
            }

            //If commitIndex > lastApplied: increment lastApplied, apply log[lastApplied] to state machine (§5.3)

            return this;
        }

        public IState Handle(RequestVoteResponse requestVoteResponse)
        {
             //todo - consolidate with AppendEntries and RequestVOte wtc
            if(requestVoteResponse.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, requestVoteResponse.Term, CurrentState.VotedFor, 
                    CurrentState.Timeout, CurrentState.Log, CurrentState.CommitIndex, CurrentState.LastApplied);
                return new Follower(nextState, _sendToSelf);
            }

            return this;
        }
    }
}