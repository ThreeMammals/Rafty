namespace Rafty.Concensus.States
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FiniteStateMachine;
    using Infrastructure;
    using Log;
    using Messages;
    using Microsoft.Extensions.Logging;
    using Node;
    using Peers;

    public sealed class Follower : IState
    {
        private readonly IFiniteStateMachine _fsm;
        private readonly ILog _log;
        private readonly IRandomDelay _random;
        private Timer _electionTimer;
        private int _messagesSinceLastElectionExpiry;
        private readonly INode _node;
        private readonly ISettings _settings;
        private readonly IRules _rules;
        private List<IPeer> _peers;
        private readonly ILogger<Follower> _logger;
        private readonly SemaphoreSlim _appendingEntries = new SemaphoreSlim(1,1);
        private bool _checkingElectionStatus;
        private bool _disposed;

        public Follower(
            CurrentState state, 
            IFiniteStateMachine stateMachine, 
            ILog log, 
            IRandomDelay random, 
            INode node, 
            ISettings settings, 
            IRules rules,
            List<IPeer> peers,
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Follower>();
            _peers = peers;
            _rules = rules;
            _random = random;
            _node = node;
            _settings = settings;
            _fsm = stateMachine;
            CurrentState = state;
            _log = log;
            ResetElectionTimer();
        }

        public CurrentState CurrentState { get; private set;}

        public async Task<AppendEntriesResponse> Handle(AppendEntries appendEntries)
        {
            try
            {
                await _appendingEntries.WaitAsync();

                var response = _rules.AppendEntriesTermIsLessThanCurrentTerm(appendEntries, CurrentState);

                if (response.shouldReturn)
                {
                    return response.appendEntriesResponse;
                }

                response =
                    await _rules.LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(appendEntries,
                        _log, CurrentState);

                if (response.shouldReturn)
                {
                    return response.appendEntriesResponse;
                }

                await _rules.DeleteAnyConflictsInLog(appendEntries, _log);

                var terms = appendEntries.Entries.Any()
                    ? string.Join(",", appendEntries.Entries.Select(x => x.Term))
                    : string.Empty;

                _logger.LogInformation(
                    $"{CurrentState.Id} as {nameof(Follower)} applying {appendEntries.Entries.Count} to log, term {terms}");

                await _rules.ApplyNewEntriesToLog(appendEntries, _log);

                var commitIndexAndLastApplied =
                    await _rules.CommitIndexAndLastApplied(appendEntries, _log, CurrentState);

                await ApplyToStateMachine(commitIndexAndLastApplied.commitIndex, commitIndexAndLastApplied.lastApplied,
                    appendEntries);

                SetLeaderId(appendEntries);

                _messagesSinceLastElectionExpiry++;

                return new AppendEntriesResponse(CurrentState.CurrentTerm, true);
            }
            finally
            {
                _appendingEntries.Release();
            }
        }

        public async Task<RequestVoteResponse> Handle(RequestVote requestVote)
        {
            var response = RequestVoteTermIsGreaterThanCurrentTerm(requestVote);

            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            response = _rules.RequestVoteTermIsLessThanCurrentTerm(requestVote, CurrentState);

            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            response = _rules.VotedForIsNotThisOrNobody(requestVote, CurrentState);

            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            response = await LastLogIndexAndLastLogTermMatchesThis(requestVote);

            _messagesSinceLastElectionExpiry++;
            
            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            return new RequestVoteResponse(false, CurrentState.CurrentTerm);
        }

        public async Task<Response<T>> Accept<T>(T command) where T : ICommand
        {
            var leader = _peers.FirstOrDefault(x => x.Id == CurrentState.LeaderId);
            if(leader != null)
            {
                _logger.LogInformation("follower forward to leader");
                return await leader.Request(command);
            }
            
            return new ErrorResponse<T>("Please retry command later. Unable to find leader.", command);
        }

        public void Stop()
        {
            _disposed = true;
            _electionTimer.Dispose();
        }

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) RequestVoteTermIsGreaterThanCurrentTerm(RequestVote requestVote)
        {
            if (requestVote.Term > CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, requestVote.Term, requestVote.CandidateId,
                    CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
                 return (new RequestVoteResponse(true, CurrentState.CurrentTerm), true);
            }

            return (null, false);
        }

        private async Task<(RequestVoteResponse requestVoteResponse, bool shouldReturn)> LastLogIndexAndLastLogTermMatchesThis(RequestVote requestVote)
        {
             if (requestVote.LastLogIndex == await _log.LastLogIndex() &&
                requestVote.LastLogTerm == await _log.LastLogTerm())
            {
                CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, requestVote.CandidateId, CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);

                return (new RequestVoteResponse(true, CurrentState.CurrentTerm), true);
            }

            return (null, false);
        }

        private async Task ApplyToStateMachine(int commitIndex, int lastApplied, AppendEntries appendEntries)
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
        
        private void ElectionTimerExpired()
        {
            try
            {
                _appendingEntries.Wait();

                if (_messagesSinceLastElectionExpiry == 0)
                {
                    _node.BecomeCandidate(CurrentState);
                }
                else
                {
                    ResetElectionTimer();
                }
            }
            finally
            {
                _appendingEntries.Release();
            }
        }

        private void SetLeaderId(AppendEntries appendEntries)
        {
            CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied, appendEntries.LeaderId);
        }

        private void ResetElectionTimer()
        {
            _messagesSinceLastElectionExpiry = 0;
            var timeout = _random.Get(_settings.MinTimeout, _settings.MaxTimeout);
            _electionTimer?.Dispose();
            _electionTimer = new Timer(x =>
            {
                if (_checkingElectionStatus)
                {
                    return;
                }

                _checkingElectionStatus = true;

                ElectionTimerExpired();

                _checkingElectionStatus = false;

            }, null, Convert.ToInt32(timeout.TotalMilliseconds), Convert.ToInt32(timeout.TotalMilliseconds));
        }
    }
}