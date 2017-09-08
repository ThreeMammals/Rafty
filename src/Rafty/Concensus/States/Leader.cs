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
        private readonly object _lock = new object();
        private bool _handled;
        private readonly List<IPeer> _peers;
        private readonly ILog _log;
        private readonly INode _node;
        private Timer _electionTimer;
        private readonly ISettings _settings;
        public long SendAppendEntriesCount;


        public Leader(
            CurrentState currentState, 
            IFiniteStateMachine fsm, 
            List<IPeer> peers, 
            ILog log, 
            INode node, 
            ISettings settings)
        {
            _settings = settings;
            _node = node;
            _log = log;
            _peers = peers;
            _fsm = fsm;
            CurrentState = currentState;
            InitialisePeerStates();
            ResetElectionTimer();
        }

        public void Stop()
        {
            _electionTimer.Dispose();
        }

        public List<PeerState> PeerStates { get; private set; }

        public CurrentState CurrentState { get; private set; }

        private int AddCommandToLog<T>(T command)
        {
            var json = JsonConvert.SerializeObject(command);
            var log = new LogEntry(json, command.GetType(), CurrentState.CurrentTerm);
            var index = _log.Apply(log);
            return index;
        }

        private bool WaitingForCommandToPersist()
        {
            return !_handled;
        }

        private void SetUpPersisting()
        {
            _handled = false;
        }

        private bool CommitedToMajority(int commited)
        {
            return commited >= (_peers.Count) / 2 + 1;
        }

        private bool Commited(PeerState peer, int index)
        {
            return peer.MatchIndex.IndexOfHighestKnownReplicatedLog == index;
        }

        private void FinishWaitingForCommandToPersist()
        {
            _handled = true;
        }

        public Response<T> Accept<T>(T command)
        {
            var index = AddCommandToLog(command);

            SetUpPersisting();
            
            while (WaitingForCommandToPersist())
            {
                var commited = 0;

                foreach(var peer in PeerStates)
                {
                    if(Commited(peer, index))
                    {
                        commited++;
                    }

                    if (CommitedToMajority(commited))
                    {
                        _fsm.Handle(command);
                        FinishWaitingForCommandToPersist();
                        break;
                    }
                }

                Thread.Sleep(_settings.HeartbeatTimeout);
            }

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

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            if (appendEntries.Term > CurrentState.CurrentTerm)
            {
                var commitIndex = CurrentState.CommitIndex;
                var lastApplied = CurrentState.LastApplied;
                if (appendEntries.LeaderCommitIndex > CurrentState.CommitIndex)
                {
                    var lastNewEntry = _log.LastLogIndex;
                    commitIndex = System.Math.Min(appendEntries.LeaderCommitIndex, lastNewEntry);
                }

                while (commitIndex > lastApplied)
                {
                    lastApplied++;
                    var log = _log.Get(lastApplied);
                    _fsm.Handle(log.CommandData);
                }

                CurrentState = new CurrentState(CurrentState.Id, appendEntries.Term,
                    CurrentState.VotedFor, commitIndex, lastApplied);

                _node.BecomeFollower(CurrentState);

                return new AppendEntriesResponse(CurrentState.CurrentTerm, true);
            }

            return new AppendEntriesResponse(CurrentState.CurrentTerm, false);
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {    
            var response = RequestVoteTermIsGreaterThanCurrentTerm(requestVote);

            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            return new RequestVoteResponse(false, CurrentState.CurrentTerm);
        }

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) RequestVoteTermIsGreaterThanCurrentTerm(RequestVote requestVote)
        {
            if (requestVote.Term > CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, requestVote.Term, requestVote.CandidateId,
                    CurrentState.CommitIndex, CurrentState.LastApplied);
                _node.BecomeFollower(CurrentState);
                return (new RequestVoteResponse(true, CurrentState.CurrentTerm), true);
            }

            return (null, false);
        }

        private void SendAppendEntries()
        {
            SendAppendEntriesCount++;

            var responses = new ConcurrentBag<AppendEntriesResponse>();

            Parallel.ForEach(PeerStates, p =>
            {
                var logsToSend = GetLogsForPeer(p.NextIndex);
              
                var appendEntriesResponse = p.Peer.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex, _log.LastLogTerm, logsToSend.Select(x => x.Item2).ToList(), CurrentState.CommitIndex));
                responses.Add(appendEntriesResponse);
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
                    }

                    if (!appendEntriesResponse.Success)
                    {
                        var nextIndex = p.NextIndex.NextLogIndexToSendToPeer <= 1 ? 1 : p.NextIndex.NextLogIndexToSendToPeer - 1;
                        if(nextIndex < 0)
                        {
                            
                        }
                        p.UpdateNextIndex(nextIndex);
                    }
                }
            });

            foreach (var appendEntriesResponse in responses)
            {
                if(appendEntriesResponse.Term > CurrentState.CurrentTerm)
                {
                    var currentState = new CurrentState(CurrentState.Id, appendEntriesResponse.Term, 
                        CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied);
                    _node.BecomeFollower(currentState);
                    return;
                }
            }
            
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

        private void ResetElectionTimer()
        {
            _electionTimer?.Dispose();
            _electionTimer = new Timer(x =>
            {
                SendAppendEntries();

            }, null, 0, Convert.ToInt32(_settings.HeartbeatTimeout));
        }

        private void InitialisePeerStates()
        {
            PeerStates = new List<PeerState>();
            foreach (var peer in _peers)
            {
                var matchIndex = new MatchIndex(peer, 0);
                var nextIndex = new NextIndex(peer, _log.LastLogIndex);
                PeerStates.Add(new PeerState(peer, matchIndex, nextIndex));
            }
        }
    }
}