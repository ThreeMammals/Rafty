using System;
using System.Linq;
using Newtonsoft.Json;
using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    public sealed class Follower : IState
    {
        private readonly ISendToSelf _sendToSelf;
        private readonly IFiniteStateMachine _fsm;

        public Follower(CurrentState state, ISendToSelf sendToSelf, IFiniteStateMachine stateMachine)
        {
            _fsm = stateMachine;
            CurrentState = state;
            _sendToSelf = sendToSelf;
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            _sendToSelf.Publish(new BeginElection());
            return new Candidate(CurrentState, _sendToSelf, _fsm);
        }

        public IState Handle(BeginElection beginElection)
        {
            throw new Exception("Follower cannot begin an election?");
        }

        public StateAndResponse Handle(AppendEntries appendEntries)
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

            //If commitIndex > lastApplied: increment lastApplied, apply log[lastApplied] to state machine (§5.3)\
            //todo - not sure if this should be an if or a while
            while(commitIndex > lastApplied)
            {
                lastApplied++;
                var log = nextState.Log.Get(lastApplied);
                //todo - json deserialise into type? Also command might need to have type as a string not Type as this
                //will get passed over teh wire? Not sure atm ;)
                _fsm.Handle(log.CommandData);
            }

            nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, nextState.CurrentTerm, 
                CurrentState.VotedFor, CurrentState.Timeout, CurrentState.Log, commitIndex, lastApplied);

            return new StateAndResponse(new Follower(nextState, _sendToSelf, _fsm), new AppendEntriesResponse(nextState.CurrentTerm, true));
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
            return new Follower(currentState, _sendToSelf, _fsm);
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
                return new Follower(nextState, _sendToSelf, _fsm);
            }

            return this;
        }

        public Response<T> Handle<T>(T command)
        {
            throw new NotImplementedException();
        }
    }
}