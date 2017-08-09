using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

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
        private readonly IFiniteStateMachine _fsm;
        private int _updated;
        private readonly object _lock = new object();
        private bool _handled;
        private readonly List<IPeer> _peers;
        private readonly ILog _log;
        private readonly INode _node;
        private Timer _electionTimer;
        private readonly ISettings _settings;
        private bool _appendingEntries;


        public Leader(CurrentState currentState, IFiniteStateMachine fsm, List<IPeer> peers, ILog log, INode node, ISettings settings)
        {
            _settings = settings;
            _node = node;
            _log = log;
            _peers = peers;
            _fsm = fsm;
            CurrentState = currentState;
            InitialisePeerStates();
            //Upon election: send initial empty AppendEntries RPCs(heartbeat) to each server
            //quite quickly if you are the leader to keep the nodes alive..
            ResetElectionTimer();
        }


        private void ResetElectionTimer()
        {
            _electionTimer?.Dispose();
            _electionTimer = _electionTimer = new Timer(x =>
            {
                SendAppendEntries();

            }, null, 0, Convert.ToInt32(_settings.HeartbeatTimeout));
        }

        public void Stop()
        {
            _electionTimer.Dispose();
        }

        private void InitialisePeerStates()
        {
            PeerStates = new List<PeerState>();
            foreach (var peer in _peers)
            {
                var matchIndex = new MatchIndex(peer, 0);
                //todo apparently you plus one to this but because i started everthing at -1 i dont think i need to?
                var nextIndex = new NextIndex(peer, _log.LastLogIndex + 1);
                PeerStates.Add(new PeerState(peer, matchIndex, nextIndex));
            }
        }

        public List<PeerState> PeerStates { get; private set; }

        public CurrentState CurrentState { get; private set; }

        private void SendAppendEntries()
        {
            var responses = new ConcurrentBag<AppendEntriesResponse>();

            Parallel.ForEach(PeerStates, p =>
            {
                var logsToSend = GetLogsForPeer(p.NextIndex);
                var appendEntriesResponse = p.Peer.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex, _log.LastLogTerm, logsToSend.Select(x => x.Item2).ToList(), CurrentState.CommitIndex));
                responses.Add(appendEntriesResponse);
                //handle response and update state accordingly?
                lock (_lock)
                {
                    if (appendEntriesResponse.Success)
                    {
                        var newMatchIndex =
                            Math.Max(p.MatchIndex.IndexOfHighestKnownReplicatedLog, logsToSend.Count > 0 ? logsToSend.Max(x => x.Item1) : 0);

                        var newNextIndex = newMatchIndex + 1;

                        if(newNextIndex < 0 || newMatchIndex < 0)
                        {
                            throw new Exception("Cannot be less than zero");
                        }

                        p.UpdateMatchIndex(newMatchIndex);
                        p.UpdateNextIndex(newNextIndex);
                        //if no logs then this is heartbeat so dont change it..
                    }

                    if (!appendEntriesResponse.Success)
                    {
                        var nextIndex = p.NextIndex.NextLogIndexToSendToPeer <= 0 ? 0 : p.NextIndex.NextLogIndexToSendToPeer - 1;
                        if(nextIndex < 0)
                        {
                            
                        }
                        p.UpdateNextIndex(nextIndex);
                    }
                }
            });

            //this code is pretty shit...sigh
            foreach (var appendEntriesResponse in responses)
            {
                //todo - consolidate with AppendEntries and RequestVOte
                if(appendEntriesResponse.Term > CurrentState.CurrentTerm)
                {
                    var currentState = new CurrentState(CurrentState.Id, appendEntriesResponse.Term, 
                        CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied);
                    _node.BecomeFollower(currentState);
                }
            }

            /* Mark log entries committed if stored on a majority of
             servers and at least one entry from current term is stored on
             a majority of servers
             If there exists an N such that N > commitIndex, a majority
             of matchIndex[i] ≥ N, and log[N].term == currentTerm:
             set commitIndex = N (§5.3, §5.4).*/
            
            var nextCommitIndex = CurrentState.CommitIndex + 1;
            var statesIndexOfHighestKnownReplicatedLogs = PeerStates.Select(x => x.MatchIndex.IndexOfHighestKnownReplicatedLog).ToList();
            var greaterOrEqualToN = statesIndexOfHighestKnownReplicatedLogs.Where(x => x >= nextCommitIndex).ToList();
            var lessThanN = statesIndexOfHighestKnownReplicatedLogs.Where(x => x < nextCommitIndex).ToList();
            if (greaterOrEqualToN.Count > lessThanN.Count)
            {
                if (_log.GetTermAtIndex(nextCommitIndex) == CurrentState.CurrentTerm)
                {
                    CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, 
                        CurrentState.VotedFor,  nextCommitIndex, CurrentState.LastApplied);
                }
            }
        }

        public Response<T> Accept<T>(T command)
        {
            //If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)
            var json = JsonConvert.SerializeObject(command);
            var log = new LogEntry(json, command.GetType(), CurrentState.CurrentTerm);
            var index = _log.Apply(log);
            //hack to make sure we dont handle a command twice? Must be a nicer way?
            _handled = false;
            _updated = 0;
            
            //ok so the idea of this loop is to wait until the heartbeat has sent this log to a majority of nodes then we commit it to the
            //state machine
            while (!_handled)
            {
                var commited = 0;
                
                foreach(var peer in PeerStates)
                {
                    if(peer.MatchIndex.IndexOfHighestKnownReplicatedLog == index)
                    {
                        commited++;
                    }

                    if (commited >= (_peers.Count) / 2 + 1)
                    {
                        _fsm.Handle(command);
                        _handled = true;
                        break;
                    }
                }
            }

            //send append entries to each server...
            return new Response<T>(_handled, command);
        }

        /// <summary>
        /// This is only public so I could test it....i feel bad will work towards not having it public
        /// </summary>
        /// <param name="nextIndex"></param>
        /// <returns></returns>
        public List<Tuple<int,LogEntry>> GetLogsForPeer(NextIndex nextIndex)
        {
            if (_log.Count > 0)
            {
                if (_log.LastLogIndex >= nextIndex.NextLogIndexToSendToPeer)
                {
                    var logs = _log.GetFrom(nextIndex.NextLogIndexToSendToPeer);
                    return logs;
                }
            }

            return new List<Tuple<int, LogEntry>>();
        }

        AppendEntriesResponse IState.Handle(AppendEntries appendEntries)
        {
            CurrentState nextState = CurrentState;

            if (appendEntries.Term >= CurrentState.CurrentTerm)
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
                while (commitIndex > lastApplied)
                {
                    lastApplied++;
                    var log = _log.Get(lastApplied);
                    //todo - json deserialise into type? Also command might need to have type as a string not Type as this
                    //will get passed over teh wire? Not sure atm ;)
                    _fsm.Handle(log.CommandData);
                }

                nextState = new CurrentState(CurrentState.Id, nextState.CurrentTerm,
                    CurrentState.VotedFor, commitIndex, lastApplied);
            }

            //todo consolidate with request vote
            if (appendEntries.Term > CurrentState.CurrentTerm)
            {
                nextState = new CurrentState(CurrentState.Id, appendEntries.Term,
                    CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied);
                _node.BecomeFollower(nextState);
                return new AppendEntriesResponse(nextState.CurrentTerm, true);
            }

            CurrentState = nextState;
            return new AppendEntriesResponse(CurrentState.CurrentTerm, false);
        }

        RequestVoteResponse IState.Handle(RequestVote requestVote)
        {    
            //todo - consolidate with AppendEntries
            if (requestVote.Term > CurrentState.CurrentTerm)
            {
                var nextState = new CurrentState(CurrentState.Id, requestVote.Term,
                    CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied);
                _node.BecomeFollower(nextState);
                return new RequestVoteResponse(true, CurrentState.CurrentTerm);
            }

            //leader cannot vote for anyone else...
            return new RequestVoteResponse(false, CurrentState.CurrentTerm);
        }
    }
}