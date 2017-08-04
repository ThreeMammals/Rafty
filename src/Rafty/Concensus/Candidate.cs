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
        private IRandomDelay _random;
        private bool _becomeLeader;

        public Candidate(CurrentState currentState, ISendToSelf sendToSelf, IFiniteStateMachine fsm, List<IPeer> peers, ILog log, IRandomDelay random)
        {
            _random = random;
            _log = log;
            _peers = peers;
            _fsm = fsm;
            _sendToSelf = sendToSelf;
            CurrentState = currentState;
        }

        public CurrentState CurrentState { get; private set;}

        public IState Handle(Timeout timeout)
        {          
            return new Candidate(CurrentState, _sendToSelf, _fsm, _peers, _log, _random);
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
            IState state = new Follower(CurrentState, _sendToSelf, _fsm, _peers, _log, _random);
            // • On conversion to candidate, start election:
            // • Reset election timer
             //this should be a random timeout which will help get the elections going at different times..
            var delay = _random.Get(100, Convert.ToInt32(CurrentState.Timeout.TotalMilliseconds));
            _sendToSelf.Publish(new Timeout(delay));
            
            var responses = new ConcurrentBag<RequestVoteResponse>();
            var states = new BlockingCollection<bool>();
            // • Send RequestVote RPCs to all other servers

            //so the idea here is that we start adding election results onto a queue and pick them off 
            //if we get a leader back then we stop looking as we dont become a leader more than once...
            //request votes...
            var tasks = new List<Task>();
            foreach (var peer in _peers)
            {
                var task = GetVote(peer, responses, states);
                tasks.Add(task);
            }

            //check if we are the leader...
            foreach (var nextState in states.GetConsumingEnumerable())
            {
                if (nextState)
                {
                    _becomeLeader = nextState;
                    break;
                }
            }

            //wait for the tasks to finish..
            Task.WaitAll(tasks.ToArray());
            
            //check if we really are the leader???
            foreach (var requestVoteResponse in responses)
            {
                 //todo - consolidate with AppendEntries and RequestVOte wtc
                if(requestVoteResponse.Term > CurrentState.CurrentTerm)
                {
                    var nextState = new CurrentState(CurrentState.Id, requestVoteResponse.Term, CurrentState.VotedFor, 
                        CurrentState.Timeout, CurrentState.CommitIndex, CurrentState.LastApplied);
                    return new Follower(nextState, _sendToSelf, _fsm, _peers, _log, _random);
                }
            }

            if (_becomeLeader)
            {
                return new Leader(CurrentState, _sendToSelf, _fsm, _peers, _log, _random);
            }
            
            return state;
        }

        private async Task GetVote(IPeer peer, ConcurrentBag<RequestVoteResponse> responses, BlockingCollection<bool> states) 
        {
            var requestVoteResponse = peer.Request(new RequestVote(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex, _log.LastLogTerm));

            responses.Add(requestVoteResponse);

            if (requestVoteResponse.VoteGranted)
            {
                lock (_lock)
                {
                    _votesThisElection++;

                    //If votes received from majority of servers: become leader
                    if (_votesThisElection >= (_peers.Count + 1) / 2 + 1)
                    {
                        //add the state to the queue to be 
                        //var state = new Leader(CurrentState, _sendToSelf, _fsm, _peers, _log, _random);
                        states.Add(true);
                    }
                }
            }
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
                return new Follower(nextState, _sendToSelf, _fsm, _peers, _log, _random);
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
                return new Follower(nextState, _sendToSelf, _fsm, _peers, _log, _random);
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