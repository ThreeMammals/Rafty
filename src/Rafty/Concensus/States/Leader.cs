namespace Rafty.Concensus
{  
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Rafty.Concensus.States;
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
        private IRules _rules;

        public Leader(
            CurrentState currentState, 
            IFiniteStateMachine fsm, 
            List<IPeer> peers, 
            ILog log, 
            INode node, 
            ISettings settings,
            IRules rules)
        {
            _rules = rules;
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


        public Response<T> Accept<T>(T command)
        {
            var index = AddCommandToLog(command);

            SetUpReplication();
            
            while (WaitingForCommandToReplicate())
            {
                var replicated = 0;

                foreach(var peer in PeerStates)
                {
                    if(Replicated(peer, index))
                    {
                        replicated++;
                    }

                    if (ReplicatedToMajority(replicated))
                    {
                        _fsm.Handle(command);
                        FinishWaitingForCommandToReplicate();
                        break;
                    }
                }

                Wait();
            }

            return new Response<T>(_handled, command);
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            if (appendEntries.Term > CurrentState.CurrentTerm)
            {
                var response = _rules.CommitIndexAndLastApplied(appendEntries, _log, CurrentState);

                ApplyToStateMachine(appendEntries, response.commitIndex, response.lastApplied);

                SetLeaderId(appendEntries);

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

        private ConcurrentBag<AppendEntriesResponse> SetUpAppendingEntries()
        {
            SendAppendEntriesCount++;
            var responses = new ConcurrentBag<AppendEntriesResponse>();
            return responses;
        }

        private AppendEntriesResponse GetAppendEntriesResponse(PeerState p, List<(int, LogEntry logEntry)> logsToSend)
        {
            var appendEntriesResponse = p.Peer.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex, _log.LastLogTerm, logsToSend.Select(x => x.logEntry).ToList(), CurrentState.CommitIndex));
            return appendEntriesResponse;
        }

        private void UpdatePeerIndexes(PeerState p, List<(int index, LogEntry logEntry)> logsToSend)
        {
            var newMatchIndex =
                Math.Max(p.MatchIndex.IndexOfHighestKnownReplicatedLog, logsToSend.Count > 0 ? logsToSend.Max(x => x.index) : 0);
            var newNextIndex = newMatchIndex + 1;
            p.UpdateMatchIndex(newMatchIndex);
            p.UpdateNextIndex(newNextIndex);
        }

        private void UpdatePeerIndexes(PeerState p)
        {
            var nextIndex = p.NextIndex.NextLogIndexToSendToPeer <= 1 ? 1 : p.NextIndex.NextLogIndexToSendToPeer - 1;
                        p.UpdateNextIndex(nextIndex);
        }

        private void UpdateIndexes(PeerState peer, List<(int index, LogEntry logEntry)> logsToSend, AppendEntriesResponse appendEntriesResponse)
        {
            lock (_lock)
            {
                if (appendEntriesResponse.Success)
                {
                    UpdatePeerIndexes(peer, logsToSend);
                } 
                else
                {
                    UpdatePeerIndexes(peer);
                }
            }
        }

        private void SendAppendEntries()
        {
            var appendEntriesResponses = SetUpAppendingEntries();

            Parallel.ForEach(PeerStates, peer =>
            {
                var logsToSend = GetLogsForPeer(peer.NextIndex);
              
                var appendEntriesResponse = GetAppendEntriesResponse(peer, logsToSend);

                appendEntriesResponses.Add(appendEntriesResponse);
                
                UpdateIndexes(peer, logsToSend, appendEntriesResponse);
            });

            var response = DoesResponseContainsGreaterTerm(appendEntriesResponses);

            if(response.conainsGreaterTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, response.newTerm, 
                        CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
                _node.BecomeFollower(CurrentState);
                return;
            }

            UpdateCommitIndex();
        }

        private (bool conainsGreaterTerm, long newTerm) DoesResponseContainsGreaterTerm(ConcurrentBag<AppendEntriesResponse> appendEntriesResponses)
        {
            foreach (var appendEntriesResponse in appendEntriesResponses)
            {
                if(appendEntriesResponse.Term > CurrentState.CurrentTerm)
                {
                    return (true, appendEntriesResponse.Term);
                }
            }

            return (false, 0);
        }

        private void UpdateCommitIndex()
        {
            var nextCommitIndex = CurrentState.CommitIndex + 1;
            var statesIndexOfHighestKnownReplicatedLogs = PeerStates.Select(x => x.MatchIndex.IndexOfHighestKnownReplicatedLog).ToList();
            var greaterOrEqualToN = statesIndexOfHighestKnownReplicatedLogs.Where(x => x >= nextCommitIndex).ToList();
            var lessThanN = statesIndexOfHighestKnownReplicatedLogs.Where(x => x < nextCommitIndex).ToList();
            if (greaterOrEqualToN.Count > lessThanN.Count)
            {
                if (_log.GetTermAtIndex(nextCommitIndex) == CurrentState.CurrentTerm)
                {
                    CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, 
                        CurrentState.VotedFor,  nextCommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
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

            _peers.ForEach(p => {
                var matchIndex = new MatchIndex(p, 0);
                var nextIndex = new NextIndex(p, _log.LastLogIndex);
                PeerStates.Add(new PeerState(p, matchIndex, nextIndex));
            });
        }

        private int AddCommandToLog<T>(T command)
        {
            var json = JsonConvert.SerializeObject(command);
            var log = new LogEntry(json, command.GetType(), CurrentState.CurrentTerm);
            var index = _log.Apply(log);
            return index;
        }

        private bool WaitingForCommandToReplicate()
        {
            return !_handled;
        }

        private void SetUpReplication()
        {
            _handled = false;
        }

        private bool ReplicatedToMajority(int commited)
        {
            return commited >= (_peers.Count) / 2 + 1;
        }

        private bool Replicated(PeerState peer, int index)
        {
            return peer.MatchIndex.IndexOfHighestKnownReplicatedLog == index;
        }

        private void FinishWaitingForCommandToReplicate()
        {
            _handled = true;
        }

        private void Wait()
        {
            Thread.Sleep(_settings.HeartbeatTimeout);
        }

        private List<(int index ,LogEntry logEntry)> GetLogsForPeer(NextIndex nextIndex)
        {
            if (_log.Count > 0)
            {
                if (_log.LastLogIndex >= nextIndex.NextLogIndexToSendToPeer)
                {
                    var logs = _log.GetFrom(nextIndex.NextLogIndexToSendToPeer);
                    return logs;
                }
            }

            return new List<(int, LogEntry)>();
        }

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) RequestVoteTermIsGreaterThanCurrentTerm(RequestVote requestVote)
        {
            if (requestVote.Term > CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, requestVote.Term, requestVote.CandidateId,
                    CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
                _node.BecomeFollower(CurrentState);
                return (new RequestVoteResponse(true, CurrentState.CurrentTerm), true);
            }

            return (null, false);
        }

        private void ApplyToStateMachine(AppendEntries appendEntries, int commitIndex, int lastApplied)
        {
            while (commitIndex > lastApplied)
            {
                lastApplied++;
                var log = _log.Get(lastApplied);
                _fsm.Handle(log.CommandData);
            }

            CurrentState = new CurrentState(CurrentState.Id, appendEntries.Term,
                CurrentState.VotedFor, commitIndex, lastApplied, CurrentState.LeaderId);
        }

        private void SetLeaderId(AppendEntries appendEntries)
        {
            CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied, appendEntries.LeaderId);
        }
    }
}