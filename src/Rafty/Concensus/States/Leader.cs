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
    using System.Diagnostics;

    public sealed class Leader : IState
    {
        private readonly IFiniteStateMachine _fsm;
        private readonly object _lock = new object();
        private bool _handled;
        private Stopwatch _stopWatch;
        Func<CurrentState, List<IPeer>> _getPeers;
        private readonly ILog _log;
        private readonly INode _node;
        private Timer _electionTimer;
        private readonly ISettings _settings;
        public long SendAppendEntriesCount;
        private IRules _rules;
        private bool _appendingEntries;

        public Leader(
            CurrentState currentState, 
            IFiniteStateMachine fsm, 
            Func<CurrentState, List<IPeer>> getPeers, 
            ILog log, 
            INode node, 
            ISettings settings,
            IRules rules)
        {
            _rules = rules;
            _settings = settings;
            _node = node;
            _log = log;
            _getPeers = getPeers;
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

        private bool ReplicationTimeout()
        {
            return _stopWatch.ElapsedMilliseconds >= _settings.CommandTimeout;
        }

        public Response<T> Accept<T>(T command) where T : ICommand
        {
            var index = AddCommandToLog(command);
            
            var peers = _getPeers(CurrentState);
            
            if(No(peers))
            {
                var log = _log.Get(index);
                ApplyToStateMachineAndUpdateCommitIndex(log);
                return new OkResponse<T>(command);
            }

            SetUpReplication();
            
            while (WaitingForCommandToReplicate())
            {
                if(ReplicationTimeout())
                {
                    return new ErrorResponse<T>("Unable to replicate command to peers due to timeout.", command);
                }

                var replicated = 0;

                foreach(var peer in PeerStates)
                {
                    if(Replicated(peer, index))
                    {
                        replicated++;
                    }

                    if (ReplicatedToMajority(replicated))
                    {
                        var log = _log.Get(index);
                        //Console.WriteLine($"Leader applying to state machine after replicated to majority, id {CurrentState.Id}");
                        _fsm.Handle(log);
                        FinishWaitingForCommandToReplicate();
                        break;
                    }
                }

                Wait();
            }

            return new OkResponse<T>(command);
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
            if(_appendingEntries == true)
            {
                return;
            }

            _appendingEntries = true;

            var peers = _getPeers(CurrentState);

            if(No(peers))
            {
                _appendingEntries = false;
                return;
            }

            if(PeerStates.Count != peers.Count)
            {
                var peersNotInPeerStates = peers.Where(p => !PeerStates.Select(x => x.Peer.Id).Contains(p.Id)).ToList();
                
                peersNotInPeerStates.ForEach(p => {
                    var matchIndex = new MatchIndex(p, 0);
                    var nextIndex = new NextIndex(p, _log.LastLogIndex);
                    PeerStates.Add(new PeerState(p, matchIndex, nextIndex));
                });
            }

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
                _appendingEntries = false;
                return;
            }

            UpdateCommitIndex();
            _appendingEntries = false;
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
                //Console.WriteLine($"leader id: {CurrentState.Id} voted for candidate: {CurrentState.VotedFor} term : {CurrentState.CurrentTerm}");
                SendAppendEntries();

            }, null, 0, Convert.ToInt32(_settings.HeartbeatTimeout));
        }

        private void InitialisePeerStates()
        {
            PeerStates = new List<PeerState>();
            var peers = _getPeers(CurrentState);
            peers.ForEach(p => {
                var matchIndex = new MatchIndex(p, 0);
                var nextIndex = new NextIndex(p, _log.LastLogIndex);
                PeerStates.Add(new PeerState(p, matchIndex, nextIndex));
            });
        }

        private int AddCommandToLog<T>(T command) where T : ICommand
        {
            var log = new LogEntry(command, command.GetType(), CurrentState.CurrentTerm);
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
            _stopWatch = Stopwatch.StartNew();
        }

        private bool ReplicatedToMajority(int commited)
        {
            var peers = _getPeers(CurrentState);
            return commited >= (peers.Count) / 2 + 1;
        }

        private bool Replicated(PeerState peer, int index)
        {
            return peer.MatchIndex.IndexOfHighestKnownReplicatedLog == index;
        }

        private void FinishWaitingForCommandToReplicate()
        {
            _handled = true;
            _stopWatch.Stop();
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
                //Console.WriteLine($"Leader applying to state machine because received ae with higher term, id {CurrentState.Id}");
                _fsm.Handle(log);
            }

            CurrentState = new CurrentState(CurrentState.Id, appendEntries.Term,
                CurrentState.VotedFor, commitIndex, lastApplied, CurrentState.LeaderId);
        }

        private void SetLeaderId(AppendEntries appendEntries)
        {
            CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied, appendEntries.LeaderId);
        }

        private bool No(List<IPeer> peers)
        {
            if(peers.Count == 0 && PeerStates.Count == 0)
            {
                return true;
            }

            return false;
        }

        private void ApplyToStateMachineAndUpdateCommitIndex(LogEntry log)
        {
            var nextCommitIndex = CurrentState.CommitIndex + 1;
            if (_log.GetTermAtIndex(nextCommitIndex) == CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, 
                    CurrentState.VotedFor,  nextCommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
            }

            //Console.WriteLine($"Leader applying to state machine because no peers, id {CurrentState.Id}");
            _fsm.Handle(log);
        }
    }
}