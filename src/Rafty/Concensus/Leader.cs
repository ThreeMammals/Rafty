namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Rafty.FiniteStateMachine;
    using Rafty.Log;

    public sealed class Leader : IState
    {
        private ISendToSelf _sendToSelf;
        private IFiniteStateMachine _fsm;
        private int _updated;
        private readonly object _lock = new object();
        private bool _handled;

        public Leader(CurrentState currentState, ISendToSelf sendToSelf, IFiniteStateMachine fsm)
        {
            _fsm = fsm;
            CurrentState = currentState;
            _sendToSelf = sendToSelf;

            //Upon election: send initial empty AppendEntries RPCs(heartbeat) to each server
            Parallel.ForEach(CurrentState.Peers, p => {
                p.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, CurrentState.Log.LastLogIndex, CurrentState.Log.LastLogTerm, new List<Log.LogEntry>(), CurrentState.CommitIndex));
            });

            //todo - is this timeout correct? does it need to be less than the followers?
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            Parallel.ForEach(CurrentState.Peers, p => {
                p.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, CurrentState.Log.LastLogIndex, CurrentState.Log.LastLogTerm, new List<Log.LogEntry>(), CurrentState.CommitIndex));
            });

            //todo - is this timeout correct? does it need to be less than the followers?
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
            return this;
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

        public Response<T> Accept<T>(T command)
        {
            //If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)
            var json = JsonConvert.SerializeObject(command);
            var log = new LogEntry(json, command.GetType(), CurrentState.CurrentTerm, CurrentState.CommitIndex);
            CurrentState.Log.Apply(log);
            //hack to make sure we dont handle a command twice? Must be a nicer way?
            _handled = false;
            //send append entries to each server...
            Parallel.ForEach(CurrentState.Peers, (p, s) => {
                
                //todo - this should not just be latest log?
                var appendEntries = new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, CurrentState.Log.LastLogIndex,
                    CurrentState.Log.LastLogTerm, new List<LogEntry>{log}, CurrentState.CommitIndex);

                var appendEntriesResponse = p.Request(appendEntries);
                
                //lock to make sure we dont do this more than once
                lock(_lock)
                {
                    if (appendEntriesResponse.Success && !_handled)
                    {
                        _updated++;
                        //If replicated to majority of servers: apply to state machine
                        if (_updated >= (CurrentState.Peers.Count + 1) / 2 + 1)
                        {
                            _fsm.Handle(command);
                            _handled = true;
                        }
                    }
                }
            });

            return new Response<T>(_handled, command);
        }
    }
}