using System.Linq;

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
        private readonly ISendToSelf _sendToSelf;
        private readonly IFiniteStateMachine _fsm;
        private int _updated;
        private readonly object _lock = new object();
        private bool _handled;

        public Leader(CurrentState currentState, ISendToSelf sendToSelf, IFiniteStateMachine fsm)
        {
            _fsm = fsm;
            CurrentState = currentState;
            _sendToSelf = sendToSelf;

            InitialisePeerStates();
            //Upon election: send initial empty AppendEntries RPCs(heartbeat) to each server
            Parallel.ForEach(CurrentState.Peers, p => {
                p.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, CurrentState.Log.LastLogIndex, CurrentState.Log.LastLogTerm, new List<Log.LogEntry>(), CurrentState.CommitIndex));
            });

            //todo - is this timeout correct? does it need to be less than the followers?
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
        }

        private void InitialisePeerStates()
        {
            PeerStates = new List<PeerState>();
            foreach (var peer in CurrentState.Peers)
            {
                var matchIndex = new MatchIndex(peer, 0);
                //todo apparently you plus one to this but because i started everthing at -1 i dont think i need to?
                var nextIndex = new NextIndex(peer, CurrentState.Log.LastLogIndex);
                PeerStates.Add(new PeerState(peer, matchIndex, nextIndex));
            }
        }

        public List<PeerState> PeerStates { get; private set; }

        public CurrentState CurrentState { get; private set; }

        public IState Handle(Timeout timeout)
        {
            //todo - is this timeout correct? does it need to be less than the followers?
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));

            IState nextState = this;

            Parallel.ForEach(PeerStates, p =>
            {
                var logsToSend = GetLogsForPeer(p.NextIndex);
                var appendEntriesResponse = p.Peer.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, CurrentState.Log.LastLogIndex, CurrentState.Log.LastLogTerm, logsToSend, CurrentState.CommitIndex));
                
                //handle response and update state accordingly?
                lock (_lock)
                {
                    if (appendEntriesResponse.Success)
                    {
                        p.UpdateNextIndex(p.NextIndex.NextLogIndexToSendToPeer + logsToSend.Count);
                        p.UpdateMatchIndex(p.MatchIndex.IndexOfHighestKnownReplicatedLog + logsToSend.Max(x => x.CurrentCommitIndex));
                    }

                    if (!appendEntriesResponse.Success)
                    {
                        p.UpdateNextIndex(p.NextIndex.NextLogIndexToSendToPeer - 1);
                    }

                    nextState = Handle(appendEntriesResponse);
                }
            });

            return nextState;
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

            //leader cannot vote for anyone else...
            return this;
        }

        public IState Handle(AppendEntriesResponse appendEntriesResponse)
        {
             //todo - consolidate with AppendEntries and RequestVOte
            if(appendEntriesResponse.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, CurrentState.Peers, appendEntriesResponse.Term, CurrentState.VotedFor, 
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
            Parallel.ForEach(PeerStates, (p, s) =>
            {
                var logs = GetLogsForPeer(p.NextIndex);
                //todo - this should not just be latest log?
                var appendEntries = new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, CurrentState.Log.LastLogIndex,
                    CurrentState.Log.LastLogTerm, logs, CurrentState.CommitIndex);

                var appendEntriesResponse = p.Peer.Request(appendEntries);
                
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

        /// <summary>
        /// This is only public so I could test it....i feel bad will work towards not having it public
        /// </summary>
        /// <param name="nextIndex"></param>
        /// <returns></returns>
        public List<LogEntry> GetLogsForPeer(NextIndex nextIndex)
        {
            if (CurrentState.Log.Count > 0)
            {
                if (CurrentState.Log.LastLogIndex >= nextIndex.NextLogIndexToSendToPeer)
                {
                    var logs = CurrentState.Log.GetFrom(nextIndex.NextLogIndexToSendToPeer);
                    return logs;
                }
            }

            return new List<LogEntry>();
        }
    }
}