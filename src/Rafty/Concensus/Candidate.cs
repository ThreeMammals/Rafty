using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rafty.FiniteStateMachine;
using Rafty.Log;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        private readonly ISendToSelf _sendToSelf;
        private int _votesThisElection;
        private readonly object _lock = new object();
        private IFiniteStateMachine _fsm;
        private List<IPeer> _peers;
        private ILog _log;

        public Candidate(CurrentState currentState, ISendToSelf sendToSelf, IFiniteStateMachine fsm, List<IPeer> peers, ILog log)
        {
            _log = log;
            _peers = peers;
            _fsm = fsm;
            _sendToSelf = sendToSelf;
            CurrentState = currentState;
        }

        public CurrentState CurrentState { get; private set;}

        public IState Handle(Timeout timeout)
        {          
            return new Candidate(CurrentState, _sendToSelf, _fsm, _peers, _log);
        }

        public IState Handle(BeginElection beginElection)
        {
             // • On conversion to candidate, start election:
            // • Increment currentTerm
            var nextTerm = CurrentState.CurrentTerm + 1;
            // • Vote for self
            _votesThisElection++;
            var votedFor = CurrentState.Id;
            CurrentState = new CurrentState(CurrentState.Id, nextTerm, votedFor,
                CurrentState.Timeout, CurrentState.CommitIndex, CurrentState.LastApplied);
            IState state = new Follower(CurrentState, _sendToSelf, _fsm, _peers, _log);
            // • On conversion to candidate, start election:
            // • Reset election timer
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
            
            var responses = new ConcurrentBag<RequestVoteResponse>();

            // • Send RequestVote RPCs to all other servers
            //todo - this might not be the right type of loop, it should be parralell but not sure about framework version
            Parallel.ForEach(_peers, (p, s) => {
                 
                var requestVoteResponse = p.Request(new RequestVote(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex, _log.LastLogTerm));

                responses.Add(requestVoteResponse);

                if (requestVoteResponse.VoteGranted)
                {
                    lock(_lock)
                    {
                        _votesThisElection++;
                        //If votes received from majority of servers: become leader
                        if (_votesThisElection >= (_peers.Count + 1) / 2 + 1)
                        {
                            //todo this gets called three times when you get elected..
                            state = new Leader(CurrentState, _sendToSelf, _fsm, _peers, _log);
                            //todo - not sure if i need s.Break() for the algo..if it is put in then technically all servers wont receive
                            //q request vote rpc?
                            //s.Break();
                        }
                    }
                }
            });

             //this code is pretty shit...sigh
            foreach (var requestVoteResponse in responses)
            {
                 //todo - consolidate with AppendEntries and RequestVOte wtc
                if(requestVoteResponse.Term > CurrentState.CurrentTerm)
                {
                    var nextState = new CurrentState(CurrentState.Id, requestVoteResponse.Term, CurrentState.VotedFor, 
                        CurrentState.Timeout, CurrentState.CommitIndex, CurrentState.LastApplied);
                    return new Follower(nextState, _sendToSelf, _fsm, _peers, _log);
                }
            }
            
            return state;
        }

        public IState Handle(AppendEntries appendEntries)
        {
            CurrentState nextState = CurrentState;

            //todo - not sure about this should a candidate apply logs from a leader on the same term when it is in candidate mode
            //for that term? Does this need to just fall into the greater than?
            if(appendEntries.Term >= CurrentState.CurrentTerm)
            {
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
            }

            //todo consolidate with request vote
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                nextState = new CurrentState(CurrentState.Id, appendEntries.Term, 
                    CurrentState.VotedFor, CurrentState.Timeout, CurrentState.CommitIndex, CurrentState.LastApplied);
                return new Follower(nextState, _sendToSelf, _fsm, _peers, _log);
            }

            //todo - hacky :(
            CurrentState = nextState;
            return this;
        }

        public IState Handle(RequestVote requestVote)
        {
            //todo - consolidate with AppendEntries
            if(requestVote.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, requestVote.Term, CurrentState.VotedFor, 
                    CurrentState.Timeout, CurrentState.CommitIndex, CurrentState.LastApplied);
                return new Follower(nextState, _sendToSelf, _fsm, _peers, _log);
            }

            //candidate cannot vote for anyone else...
            return this;
        }

        public Response<T> Accept<T>(T command)
        {
            throw new NotImplementedException();
        }
    }
}