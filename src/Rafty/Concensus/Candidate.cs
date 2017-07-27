using System;
using System.Threading.Tasks;
using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        private readonly ISendToSelf _sendToSelf;
        private int _votesThisElection;
        private readonly object _lock = new object();
        private IFiniteStateMachine _fsm;

        public Candidate(CurrentState currentState, ISendToSelf sendToSelf, IFiniteStateMachine fsm)
        {
            _fsm = fsm;
            _sendToSelf = sendToSelf;
            // • On conversion to candidate, start election:
            // • Increment currentTerm
            var nextTerm = currentState.CurrentTerm + 1;
            // • Vote for self
            _votesThisElection++;
            var votedFor = currentState.Id;
            var nextState = new CurrentState(currentState.Id, currentState.Peers, nextTerm, votedFor,
                currentState.Timeout, currentState.Log, currentState.CommitIndex, currentState.LastApplied);
            CurrentState = nextState;
        }

        public CurrentState CurrentState { get; private set;}

        public IState Handle(Timeout timeout)
        {
            return new Candidate(CurrentState, _sendToSelf, _fsm);
        }

        public IState Handle(BeginElection beginElection)
        {
            IState state = new Follower(CurrentState, _sendToSelf, _fsm);
            // • On conversion to candidate, start election:
            // • Reset election timer
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
            
            // • Send RequestVote RPCs to all other servers
            //todo - this might not be the right type of loop, it should be parralell but not sure about framework version
            Parallel.ForEach(CurrentState.Peers, (p, s) => {
                 
                var requestVoteResponse = p.Request(new RequestVote(CurrentState.CurrentTerm, CurrentState.Id, CurrentState.Log.LastLogIndex, CurrentState.Log.LastLogTerm));

                if (requestVoteResponse.VoteGranted)
                {
                    lock(_lock)
                    {
                        _votesThisElection++;
                        //If votes received from majority of servers: become leader
                        if (_votesThisElection >= (CurrentState.Peers.Count + 1) / 2 + 1)
                        {
                            state = new Leader(CurrentState, _sendToSelf, _fsm);
                            //todo - not sure if i need s.Break() for the algo..if it is put in then technically all servers wont receive
                            //q request vote rpc?
                            //s.Break();
                        }
                    }
                }
            });

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

            //todo - hacky :(
            CurrentState = nextState;
            return this;
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

            //candidate cannot vote for anyone else...
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
            throw new NotImplementedException();
        }
    }
}