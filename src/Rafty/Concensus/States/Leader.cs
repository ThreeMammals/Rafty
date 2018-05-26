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
    using Microsoft.Extensions.Logging;

    public sealed class Leader : IState
    {
        private readonly IFiniteStateMachine _fsm;
        Func<CurrentState, List<IPeer>> _getPeers;
        private readonly ILog _log;
        private readonly INode _node;
        private readonly ISettings _settings;
        private IRules _rules;
        private readonly object _lock = new object();
        private bool _handled;
        private Stopwatch _stopWatch;
        private Timer _electionTimer;
        public long SendAppendEntriesCount;
        private bool _appendingEntries;
        private ILogger<Leader> _logger;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1,1);

        public Leader(
            CurrentState currentState, 
            IFiniteStateMachine fsm, 
            Func<CurrentState, List<IPeer>> getPeers, 
            ILog log, 
            INode node, 
            ISettings settings,
            IRules rules,
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Leader>();
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

        public List<PeerState> PeerStates { get; private set; }

        public CurrentState CurrentState { get; private set; }

        public void Stop()
        {
            _electionTimer.Dispose();
        }

        public async Task<Response<T>> Accept<T>(T command) where T : ICommand
        {
            _logger.LogInformation($"{CurrentState.Id} leader accepted command");

            var indexOfCommand = await AddCommandToLog(command);
            
            var peers = _getPeers(CurrentState);
            
            if(No(peers))
            {
                var log = await _log.Get(indexOfCommand);
                await ApplyToStateMachineAndUpdateCommitIndex(log);
                return Ok(command);
            }

            return await Replicate(command, indexOfCommand);
        }

        public async Task<AppendEntriesResponse> Handle(AppendEntries appendEntries)
        {
            if (appendEntries.Term > CurrentState.CurrentTerm)
            {
                var response = await _rules.CommitIndexAndLastApplied(appendEntries, _log, CurrentState);

                await ApplyToStateMachine(appendEntries, response.commitIndex, response.lastApplied);

                SetLeaderId(appendEntries);

                _node.BecomeFollower(CurrentState);
                
                return new AppendEntriesResponse(CurrentState.CurrentTerm, true);
            }

            return new AppendEntriesResponse(CurrentState.CurrentTerm, false);
        }

        public async Task<RequestVoteResponse> Handle(RequestVote requestVote)
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

        private async Task<AppendEntriesResponse> GetAppendEntriesResponse(PeerState p, List<(int, LogEntry logEntry)> logsToSend)
        {
            var appendEntriesResponse = await p.Peer.Request(new AppendEntries(CurrentState.CurrentTerm, CurrentState.Id, await _log.LastLogIndex(), await _log.LastLogTerm(), logsToSend.Select(x => x.logEntry).ToList(), CurrentState.CommitIndex));

            if (logsToSend.Any())
            {
                _logger.LogInformation($"id: {CurrentState.Id} received append entries failure from peer: {p.Peer.Id}");
            }

            return appendEntriesResponse;
        }

        private void UpdatePeerIndexes(PeerState p, List<(int index, LogEntry logEntry)> logsToSend)
        {
            _logger.LogInformation($"leaderid: {CurrentState.Id}, logs to send count {logsToSend.Count} to id: {p.Peer.Id}");

            if(logsToSend.Any())
            {
                _logger.LogInformation($"leaderid: {CurrentState.Id}, replicated - index: {logsToSend[0].index}, id: {p.Peer.Id}");
            }
            var newMatchIndex =
                Math.Max(p.MatchIndex.IndexOfHighestKnownReplicatedLog, logsToSend.Count > 0 ? logsToSend.Max(x => x.index) : 0);
            var newNextIndex = newMatchIndex + 1;
            
            _logger.LogInformation($"leaderid: {CurrentState.Id}, id: {p.Peer.Id}, newMatchIndex: {newMatchIndex}");
            _logger.LogInformation($"leaderid: {CurrentState.Id}id: {p.Peer.Id}, newNextIndex: {newNextIndex}");

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
            if (appendEntriesResponse.Success)
            {
                if (logsToSend.Any())
                {
                    _logger.LogInformation($"id: {CurrentState.Id} received append entries success from {peer.Peer.Id}");
                }
                UpdatePeerIndexes(peer, logsToSend);
            } 
            else
            {
                if (logsToSend.Any())
                {
                    _logger.LogInformation($"id: {CurrentState.Id} received append entries fail from {peer.Peer.Id}");
                }

                UpdatePeerIndexes(peer);
            }
        }

        private async Task SendAppendEntries(int electionTimerId)
        {
            //await _semaphore.WaitAsync();

            var peers = _getPeers(CurrentState);

            if(No(peers))
            {
                //_semaphore.Release();
                return;
            }

            if(PeerStates.Count != peers.Count)
            {
                var peersNotInPeerStates = peers.Where(p => !PeerStates.Select(x => x.Peer.Id).Contains(p.Id)).ToList();
                    
                peersNotInPeerStates.ForEach(async p => {
                    var matchIndex = new MatchIndex(p, 0);
                    var nextIndex = new NextIndex(p, await _log.LastLogIndex());
                    PeerStates.Add(new PeerState(p, matchIndex, nextIndex));
                });
            }

            var appendEntriesResponses = SetUpAppendingEntries();

            async Task Do(PeerState peer)
            {
                var logsToSend = await GetLogsForPeer(peer.NextIndex);

                if(logsToSend.Any())
                {
                    _logger.LogInformation($"leaderid: {CurrentState.Id}, electionTimerId: {electionTimerId}, sending some logs {logsToSend.Count} to {peer.Peer.Id}, this should only happen once?");
                }

                var appendEntriesResponse = await GetAppendEntriesResponse(peer, logsToSend);

                appendEntriesResponses.Add(appendEntriesResponse);

                UpdateIndexes(peer, logsToSend, appendEntriesResponse);
            }

            var tasks = new Task[PeerStates.Count];
            for (int i = 0; i < tasks.Length; i++)
            {
                var peerState = PeerStates[i];
                tasks[i] = Do(peerState);
            }

            //Parallel.ForEach(PeerStates, async peer => await Do(peer));

            await Task.WhenAll(tasks);

            var response = DoesResponseContainsGreaterTerm(appendEntriesResponses);

            if(response.conainsGreaterTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, response.newTerm, 
                        CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
                _node.BecomeFollower(CurrentState);
                //_semaphore.Release();
                return;
            }

            await UpdateCommitIndex();
            
            //_semaphore.Release();
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

        private async Task UpdateCommitIndex()
        {
            var nextCommitIndex = CurrentState.CommitIndex + 1;
            var statesIndexOfHighestKnownReplicatedLogs = PeerStates.Select(x => x.MatchIndex.IndexOfHighestKnownReplicatedLog).ToList();
            var greaterOrEqualToN = statesIndexOfHighestKnownReplicatedLogs.Where(x => x >= nextCommitIndex).ToList();
            var lessThanN = statesIndexOfHighestKnownReplicatedLogs.Where(x => x < nextCommitIndex).ToList();
            if (greaterOrEqualToN.Count > lessThanN.Count)
            {
                if (await _log.GetTermAtIndex(nextCommitIndex) == CurrentState.CurrentTerm)
                {
                    CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, 
                        CurrentState.VotedFor,  nextCommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
                }
            }
        }

        private int _electionTimerId;

        private void ResetElectionTimer()
        {
            _electionTimer?.Dispose();
            _electionTimer = new Timer(async x =>
            {
                _electionTimerId++;
                _logger.LogInformation($"id: {CurrentState.Id}, _electionTimerId:{_electionTimerId}, _appendingEntries was {_appendingEntries}");

                if (_appendingEntries)
                {
                    return;
                }

                _appendingEntries = true;
                _logger.LogInformation($"id: {CurrentState.Id}, _electionTimerId:{_electionTimerId}, sending append entries should be at most every 500ms");
                await SendAppendEntries(_electionTimerId);
                _logger.LogInformation($"id: {CurrentState.Id}, _electionTimerId:{_electionTimerId}, finished sending append entries");
                
                _appendingEntries = false;

            }, null, 0, Convert.ToInt32(_settings.HeartbeatTimeout));
        }

        private void InitialisePeerStates()
        {
            PeerStates = new List<PeerState>();
            var peers = _getPeers(CurrentState);
            peers.ForEach(async p => {
                var matchIndex = new MatchIndex(p, 0);
                var nextIndex = new NextIndex(p, await _log.LastLogIndex());
                PeerStates.Add(new PeerState(p, matchIndex, nextIndex));
            });
        }

        private async Task<int> AddCommandToLog<T>(T command) where T : ICommand
        {
            var log = new LogEntry(command, command.GetType(), CurrentState.CurrentTerm);
            _logger.LogInformation($"{CurrentState.Id} leader logging command");
            var index = await _log.Apply(log);
            _logger.LogInformation($"{CurrentState.Id} leader logging command index: {index}");
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
            var replicated =  peer.MatchIndex.IndexOfHighestKnownReplicatedLog == index;
            if (!replicated)
            {
                _logger.LogInformation(
                    $"id: {CurrentState.Id} has not replicated to peerId: {peer.Peer.Id}, index: {index} and IndexOfHighestKnownReplicatedLog: {peer.MatchIndex.IndexOfHighestKnownReplicatedLog}");
            }

            return replicated;
        }

        private void StopWaitingForCommandToReplicate()
        {
            _handled = true;
            _stopWatch.Stop();
        }

        private void Wait()
        {
            Thread.Sleep(_settings.HeartbeatTimeout);
        }

        private async Task<List<(int index ,LogEntry logEntry)>> GetLogsForPeer(NextIndex nextIndex)
        {
            if (await _log.Count() > 0)
            {
                var lastLogIndex = await _log.LastLogIndex();

                _logger.LogInformation($"lastLogIndex: {lastLogIndex}");
                _logger.LogInformation($"nextIndex.NextLogIndexToSendToPeer: {nextIndex.NextLogIndexToSendToPeer}");

                if (lastLogIndex >= nextIndex.NextLogIndexToSendToPeer)
                {
                    var logs = await _log.GetFrom(nextIndex.NextLogIndexToSendToPeer);
                    
                    _logger.LogInformation($"using {nextIndex.NextLogIndexToSendToPeer} leader got logs.Count: {logs.Count}");

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

        private async Task ApplyToStateMachine(AppendEntries appendEntries, int commitIndex, int lastApplied)
        {
            while (commitIndex > lastApplied)
            {
                lastApplied++;
                var log = await _log.Get(lastApplied);
                await _fsm.Handle(log);
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

        private async Task ApplyToStateMachineAndUpdateCommitIndex(LogEntry log)
        {
            var nextCommitIndex = CurrentState.CommitIndex + 1;
            if (await _log.GetTermAtIndex(nextCommitIndex) == CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, 
                    CurrentState.VotedFor,  nextCommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
            }

            await _fsm.Handle(log);
        }

        private OkResponse<T> Ok<T>(T command)
        {
            _logger.LogInformation($"id: {CurrentState.Id} returned OK from replicating command");
            return new OkResponse<T>(command);
        }

        private async Task<ErrorResponse<T>> UnableDueToTimeout<T>(T command, int indexOfCommand)
        {
            DecrementIndexesOfAnyPeersCommandReplicatedTo(indexOfCommand);
            await _log.Remove(indexOfCommand);
            _appendingEntries = false;
            return new ErrorResponse<T>("Unable to replicate command to peers due to timeout.", command);
        }

        private void DecrementIndexesOfAnyPeersCommandReplicatedTo(int indexOfCommand)
        {
            var peersThatHaveLogReplicated = PeerStates.Where(x => x.MatchIndex.IndexOfHighestKnownReplicatedLog == indexOfCommand).ToList();
            peersThatHaveLogReplicated.ForEach(p => {
                var currentMatchIndex = p.MatchIndex.IndexOfHighestKnownReplicatedLog - 1;
                var currentNextIndex = p.NextIndex.NextLogIndexToSendToPeer - 1;
                p.UpdateMatchIndex(currentMatchIndex);
                p.UpdateNextIndex(currentNextIndex);
            });
        }

        private async Task<Response<T>> Replicate<T>(T command, int indexOfCommand)
        {
            SetUpReplication();
            
            while (WaitingForCommandToReplicate())
            {
                if(ReplicationTimeout())
                {
                    _logger.LogInformation($"id: {CurrentState.Id} Replication timed out..");
                    return await UnableDueToTimeout(command, indexOfCommand);
                }

                var replicated = 0;

                foreach(var peer in PeerStates)
                {
                    if(Replicated(peer, indexOfCommand))
                    {
                        replicated++;
                    }

                    if (ReplicatedToMajority(replicated))
                    {
                        var log = await _log.Get(indexOfCommand);
                        await _fsm.Handle(log);
                        StopWaitingForCommandToReplicate();
                        break;
                    }
                }

                Wait();
            }

            return Ok(command);
        }

        private bool ReplicationTimeout()
        {
            return _stopWatch.ElapsedMilliseconds >= _settings.CommandTimeout;
        }
    }
}