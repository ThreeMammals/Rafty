using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rafty.FiniteStateMachine;
using Rafty.Log;

namespace Rafty.Concensus
{
    public sealed class Follower : IState
    {
        private readonly ISendToSelf _sendToSelf;
        private readonly IFiniteStateMachine _fsm;
        private List<IPeer> _peers;
        private ILog _log;

        public Follower(CurrentState state, ISendToSelf sendToSelf, IFiniteStateMachine stateMachine, List<IPeer> peers, ILog log)
        {
            _peers = peers;
            _fsm = stateMachine;
            CurrentState = state;
            _sendToSelf = sendToSelf;
            _log = log;
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            return new Candidate(CurrentState, _sendToSelf, _fsm, _peers, _log);
        }

        public IState Handle(BeginElection beginElection)
        {
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
            return this;
        }

        public IState Handle(AppendEntries appendEntries)
        {
            CurrentState nextState = CurrentState;
            //todo consolidate with request vote
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                nextState = new CurrentState(CurrentState.Id, appendEntries.Term, 
                    CurrentState.VotedFor, CurrentState.Timeout, CurrentState.CommitIndex, CurrentState.LastApplied);
            }

            //If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)
            var commitIndex = CurrentState.CommitIndex;
            var lastApplied = CurrentState.LastApplied;
            if (appendEntries.LeaderCommitIndex > CurrentState.CommitIndex)
            {
                //This only works because of the code in the node class that handles the message first (I think..im a bit stupid)
                var lastNewEntry = _log.LastLogIndex;
                commitIndex = System.Math.Min(appendEntries.LeaderCommitIndex, lastNewEntry);
            }

            //If commitIndex > lastApplied: increment lastApplied, apply log[lastApplied] to state machine (§5.3)\
            //todo - not sure if this should be an if or a while
            while(commitIndex > lastApplied)
            {
                lastApplied++;
                var log = _log.Get(lastApplied);
                //todo - json deserialise into type? Also command might need to have type as a string not Type as this
                //will get passed over teh wire? Not sure atm ;)
                _fsm.Handle(log.CommandData);
            }

            nextState = new CurrentState(CurrentState.Id, nextState.CurrentTerm, 
                CurrentState.VotedFor, CurrentState.Timeout, commitIndex, lastApplied);

            return new Follower(nextState, _sendToSelf, _fsm, _peers, _log);
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
            var currentState = new CurrentState(CurrentState.Id, term, requestVote.CandidateId, CurrentState.Timeout, 
                CurrentState.CommitIndex, CurrentState.LastApplied);
                
            return new Follower(currentState, _sendToSelf, _fsm, _peers, _log);
        }

        public Response<T> Accept<T>(T command)
        {
            throw new NotImplementedException();
        }
    }
}