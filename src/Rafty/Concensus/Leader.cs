using System.Collections.Concurrent;
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
        private List<IPeer> _peers;
        private ILog _log;
        private IRandomDelay _random;
        public Leader(CurrentState currentState, ISendToSelf sendToSelf, IFiniteStateMachine fsm, List<IPeer> peers, ILog log, IRandomDelay random)
        {
            _random = random;
            _log = log;
            _peers = peers;
            _fsm = fsm;
            CurrentState = currentState;
            _sendToSelf = sendToSelf;

            InitialisePeerStates();
            //Upon election: send initial empty AppendEntries RPCs(heartbeat) to each server
            //do this straight away with no timeout and dont call
            //send to self...
            //todo - this will error at the moment cos of log crap
            Handle(new Timeout());

            //todo - is this timeout correct? does it need to be less than the followers? Feel like this should tick
            //quite quickly if you are the leader to keep the nodes alive..
            var smallestLeaderTimeout = 500;
            if(CurrentState.Timeout.TotalMilliseconds < smallestLeaderTimeout)
            {
                _sendToSelf.Publish(new Timeout(TimeSpan.FromMilliseconds(smallestLeaderTimeout)));
            }
                
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
        }

        private void InitialisePeerStates()
        {
            PeerStates = new List<PeerState>();
            foreach (var peer in _peers)
            {
                var matchIndex = new MatchIndex(peer, 0);
                //todo apparently you plus one to this but because i started everthing at -1 i dont think i need to?
                var nextIndex = new NextIndex(peer, _log.LastLogIndex);
                PeerStates.Add(new PeerState(peer, matchIndex, nextIndex));
            }
        }

        public List<PeerState> PeerStates { get; private set; }

        public CurrentState CurrentState { get; private set; }

        public IState Handle(Timeout timeout)
        {
            //todo - is this timeout correct? does it need to be less than the followers?var smallestLeaderTimeout = 500;
            var smallestLeaderTimeout = 500;
            if(CurrentState.Timeout.TotalMilliseconds < smallestLeaderTimeout)
            {
                _sendToSelf.Publish(new Timeout(TimeSpan.FromMilliseconds(smallestLeaderTimeout)));
            }
                
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));

            IState nextState = this;

            var responses = new ConcurrentBag<AppendEntriesResponse>();

            Parallel.ForEach(PeerStates, p =>
            {
                var logsToSend = GetLogsForPeer(p.NextIndex);
                var appendEntriesResponse = p.Peer.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex, _log.LastLogTerm, logsToSend, CurrentState.CommitIndex));
                responses.Add(appendEntriesResponse);
                //handle response and update state accordingly?
                lock (_lock)
                {
                    if (appendEntriesResponse.Success)
                    {
                        p.UpdateNextIndex(p.NextIndex.NextLogIndexToSendToPeer + logsToSend.Count);
                        p.UpdateMatchIndex(logsToSend.Count > 0 ? p.MatchIndex.IndexOfHighestKnownReplicatedLog + logsToSend.Max(x => x.CurrentCommitIndex) : -1);
                    }

                    if (!appendEntriesResponse.Success)
                    {
                        p.UpdateNextIndex(p.NextIndex.NextLogIndexToSendToPeer - 1);
                    }
                }
            });

            //this code is pretty shit...sigh
            foreach (var appendEntriesResponse in responses)
            {
                //todo - consolidate with AppendEntries and RequestVOte
                if(appendEntriesResponse.Term > CurrentState.CurrentTerm)
                {
                    var currentState = new CurrentState(CurrentState.Id, appendEntriesResponse.Term, CurrentState.VotedFor, 
                        CurrentState.Timeout, CurrentState.CommitIndex, CurrentState.LastApplied);
                    return new Follower(currentState, _sendToSelf, _fsm, _peers, _log, _random);
                }
            }

            /* Mark log entries committed if stored on a majority of
             servers and at least one entry from current term is stored on
             a majority of servers
             If there exists an N such that N > commitIndex, a majority
             of matchIndex[i] ≥ N, and log[N].term == currentTerm:
             set commitIndex = N (§5.3, §5.4).*/
            var n = CurrentState.CommitIndex + 1;
            var statesIndexOfHighestKnownReplicatedLogs = PeerStates.Select(x => x.MatchIndex.IndexOfHighestKnownReplicatedLog).ToList();
            var greaterOrEqualToN = statesIndexOfHighestKnownReplicatedLogs.Where(x => x >= n).ToList();
            var lessThanN = statesIndexOfHighestKnownReplicatedLogs.Where(x => x < n).ToList();
            if (greaterOrEqualToN.Count > lessThanN.Count)
            {
                if (_log.GetTermAtIndex(n) == CurrentState.CurrentTerm)
                {
                    CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, CurrentState.VotedFor, CurrentState.Timeout,  n, CurrentState.LastApplied);
                }
            }

            return nextState;
        }

        public IState Handle(BeginElection beginElection)
        {
            return this;
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

            //leader cannot vote for anyone else...
            return this;
        }

        public Response<T> Accept<T>(T command)
        {
            //If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)
            var json = JsonConvert.SerializeObject(command);
            var log = new LogEntry(json, command.GetType(), CurrentState.CurrentTerm, CurrentState.CommitIndex);
            _log.Apply(log);
            //hack to make sure we dont handle a command twice? Must be a nicer way?
            _handled = false;
            //send append entries to each server...
            Parallel.ForEach(PeerStates, (p, s) =>
            {
                var logs = GetLogsForPeer(p.NextIndex);
                //todo - this should not just be latest log?
                var appendEntries = new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex,
                    _log.LastLogTerm, logs, CurrentState.CommitIndex);

                var appendEntriesResponse = p.Peer.Request(appendEntries);
                
                //lock to make sure we dont do this more than once
                lock(_lock)
                {
                    if (appendEntriesResponse.Success && !_handled)
                    {
                        _updated++;
                        //If replicated to majority of servers: apply to state machine
                        if (_updated >= (_peers.Count + 1) / 2 + 1)
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
            if (_log.Count > 0)
            {
                if (_log.LastLogIndex >= nextIndex.NextLogIndexToSendToPeer)
                {
                    var logs = _log.GetFrom(nextIndex.NextLogIndexToSendToPeer);
                    return logs;
                }
            }

            return new List<LogEntry>();
        }
    }
}