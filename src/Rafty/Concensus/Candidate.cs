using System;
using System.Threading.Tasks;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        private readonly ISendToSelf _sendToSelf;
        private int _votesThisElection;
        private readonly object _lock = new object();

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
                currentState.Timeout, currentState.Log, currentState.CommitIndex);
            CurrentState = nextState;
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            return new Candidate(CurrentState, _sendToSelf);
        }

        public IState Handle(BeginElection beginElection)
        {
            IState state = new Follower(CurrentState, _sendToSelf);
            // • On conversion to candidate, start election:
            // • Reset election timer
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
            
            // • Send RequestVote RPCs to all other servers
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
                            state = new Leader(CurrentState, _sendToSelf);
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
            //todo consolidate with request vote
            if(appendEntries.Term > CurrentState.CurrentTerm)
            {
                //If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)
                var commitIndex = CurrentState.CommitIndex;
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