using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rafty.Concensus.States;
using Rafty.FiniteStateMachine;
using Rafty.Log;

namespace Rafty.Concensus
{
    using Microsoft.Extensions.Logging;

    public sealed class Candidate : IState
    {
        private readonly IFiniteStateMachine _fsm;
        private readonly List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _random;
        private readonly ISettings _settings;
        private readonly INode _node;
        private readonly IRules _rules;
        private bool _becomeLeader;
        private bool _electioneering;
        private Timer _electionTimer;
        private bool _requestVoteResponseWithGreaterTerm;
        private int _votesThisElection;
        private readonly object _lock = new object();
        private int _applied;
        private ILogger<Candidate> _logger;
        private bool _doingElection;

        public Candidate(
            CurrentState currentState, 
            IFiniteStateMachine fsm, 
            List<IPeer> peers, 
            ILog log, 
            IRandomDelay random, 
            INode node, 
            ISettings settings,
            IRules rules,
            ILoggerFactory loggerFactory)
        {
            try
            {
                _logger = loggerFactory.CreateLogger<Candidate>();
            }
            catch (ObjectDisposedException e)
            {
                //happens because asp.net shuts down services sometimes before onshutdown
            }

            _rules = rules;
            _random = random;
            _node = node;
            _settings = settings;
            _log = log;
            _peers = peers;
            _fsm = fsm;
            CurrentState = currentState;
            StartElectioneering();
            ResetElectionTimer();
        }

        public CurrentState CurrentState { get; private set;}

        public void BeginElection()
        {
            SetUpElection();

            if (No(_peers))
            {
                StopElectioneering();
                BecomeLeader();
                return;
            }

            DoElection();

            StopElectioneering();

            if (WonElection())
            {
                BecomeLeader();
                return;
            }

            BecomeFollower();
        }

        public async Task<AppendEntriesResponse> Handle(AppendEntries appendEntries)
        {
            var response = _rules.AppendEntriesTermIsLessThanCurrentTerm(appendEntries, CurrentState);

            if(response.shouldReturn)
            {
                return response.appendEntriesResponse;
            }

            response = await _rules.LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(appendEntries, _log, CurrentState);

            if(response.shouldReturn)
            {
                return response.appendEntriesResponse;
            }
          
            await _rules.DeleteAnyConflictsInLog(appendEntries, _log, _logger, CurrentState.Id);

            if (_applied > 1 && appendEntries.Entries.Any())
            {
                Console.WriteLine("WTF?");
            }

            _applied++;

            _logger.LogInformation($"{CurrentState.Id} as {nameof(Candidate)} applying entry to log");

            await _rules.ApplyNewEntriesToLog(appendEntries, _log, _logger, CurrentState.Id);

            var commitIndexAndLastApplied = await _rules.CommitIndexAndLastApplied(appendEntries, _log, CurrentState);

            await ApplyToStateMachine(commitIndexAndLastApplied.commitIndex, commitIndexAndLastApplied.lastApplied);

            SetLeaderId(appendEntries);
            
            AppendEntriesTermIsGreaterThanCurrentTerm(appendEntries);

            return new AppendEntriesResponse(CurrentState.CurrentTerm, true);
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

            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            return new RequestVoteResponse(false, CurrentState.CurrentTerm);
        }

        public async Task<Response<T>> Accept<T>(T command) where T : ICommand
        {
            _logger.LogInformation("candidate dont forward to leader");
           return new ErrorResponse<T>("Please retry command later. Currently electing new a new leader.", command);
        }

        public void Stop()
        {
            _electionTimer.Dispose();
        }

        private List<Task> GetVotes(BlockingCollection<RequestVoteResponse> requestVoteResponses)
        {
            var getVotes = new List<Task>();

            _peers.ForEach(p => {
                getVotes.Add(RequestVote(p, requestVoteResponses));
            });

            return getVotes;
        }

        private async Task CountVotes(BlockingCollection<RequestVoteResponse> requestVoteResponses)
        {
            var receivedResponses = 0;

            var countedVotes = new List<Task>();

            foreach (var requestVoteResponse in requestVoteResponses.GetConsumingEnumerable())
            {
                var countedVote = CountVote(requestVoteResponse);

                countedVotes.Add(countedVote);

                receivedResponses++;

                if (ReceivedMaximumResponses(receivedResponses))
                {
                    break;
                }
            }

            Task.WaitAll(countedVotes.ToArray());
        }

        private bool ReceivedMaximumResponses(int receivedResponses)
        {
            return receivedResponses >= _peers.Count;
        }

        private async Task CountVote(RequestVoteResponse requestVoteResponse)
        {
            if (ResponseContainsTermGreaterThanCurrentTerm(requestVoteResponse))
            {
                BecomeFollowerAfterElectionFinishes(requestVoteResponse);
            }

            if (requestVoteResponse.VoteGranted)
            {
                lock (_lock)
                {
                    _votesThisElection++;

                    if (HasMajority())
                    {
                        ShouldBecomeLeader();
                    }
                }
            }
        }

        private bool ResponseContainsTermGreaterThanCurrentTerm(RequestVoteResponse requestVoteResponse)
        {
            return requestVoteResponse.Term > CurrentState.CurrentTerm;
        }

        private void BecomeFollowerAfterElectionFinishes(RequestVoteResponse requestVoteResponse)
        {
                CurrentState = new CurrentState(CurrentState.Id, requestVoteResponse.Term,
                    CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);

                _requestVoteResponseWithGreaterTerm = true;
        }

        private bool HasMajority()
        {
            return _votesThisElection >= (_peers.Count + 1) / 2 + 1;
        }

        private void ShouldBecomeLeader()
        {
            _becomeLeader = true;
        }

        private async Task RequestVote(IPeer peer, BlockingCollection<RequestVoteResponse> requestVoteResponses) 
        {
            var requestVoteResponse = await peer.Request(new RequestVote(CurrentState.CurrentTerm, CurrentState.Id, await _log.LastLogIndex(), await _log.LastLogTerm()));
            requestVoteResponses.Add(requestVoteResponse);
        }

        private void ElectionTimerExpired()
        {
            if (NotElecting())
            {
                BecomeCandidate();
            }
            else
            {
                ResetElectionTimer();
            }
        }

        private bool NotElecting()
        {
            return !_electioneering;
        }

        private void ResetElectionTimer()
        {
            var timeout = _random.Get(_settings.MinTimeout, _settings.MaxTimeout);
            _electionTimer?.Dispose();
            _electionTimer = new Timer(x =>
            {
                if (_doingElection)
                {
                    return;
                }

                _doingElection = true;

                ElectionTimerExpired();

                _doingElection = false;

            }, null, Convert.ToInt32(timeout.TotalMilliseconds), Convert.ToInt32(timeout.TotalMilliseconds));
        }
        
        private async Task ApplyToStateMachine(int commitIndex, int lastApplied)
        {
            while (commitIndex > lastApplied)
            {
                lastApplied++;
                var log = await _log.Get(lastApplied);
                await _fsm.Handle(log);
            }

            CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm,
                CurrentState.VotedFor, commitIndex, lastApplied, CurrentState.LeaderId);
        }

        private void AppendEntriesTermIsGreaterThanCurrentTerm(AppendEntries appendEntries)
        {
            if (appendEntries.Term > CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, appendEntries.Term, CurrentState.VotedFor,
                    CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);

                BecomeFollower();
            }
        }

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) RequestVoteTermIsGreaterThanCurrentTerm(RequestVote requestVote)
        {
            if (requestVote.Term > CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, requestVote.Term, requestVote.CandidateId,
                    CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
                BecomeFollower();
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
                BecomeFollower();
                return (new RequestVoteResponse(true, CurrentState.CurrentTerm), true);
            }

            return (null, false);
        }

        private void StartElectioneering()
        {
            _electioneering = true;
        }

        private void StopElectioneering()
        {
            _electioneering = false;
        }
        
        private bool No(List<IPeer> peers)
        {
            return peers.Count == 0;
        }

        private void SetUpElection()
        {
            var nextTerm = CurrentState.CurrentTerm + 1;

            var votedFor = CurrentState.Id;

            _votesThisElection++;

            CurrentState = new CurrentState(CurrentState.Id, nextTerm, votedFor, 
                CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);
        }

        private void SetLeaderId(AppendEntries appendEntries)
        {
            CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied, appendEntries.LeaderId);
        }
        
        private void DoElection()
        {
            var requestVoteResponses = new BlockingCollection<RequestVoteResponse>();
            
            var votes = GetVotes(requestVoteResponses);

            var countVotes = CountVotes(requestVoteResponses);

            Task.WaitAll(votes.ToArray());

            countVotes.Wait();
        }

        private bool WonElection()
        {
            return _becomeLeader && !_requestVoteResponseWithGreaterTerm && CurrentState.VotedFor == CurrentState.Id;
        }

        private void BecomeLeader()
        {
            _node.BecomeLeader(CurrentState);
        }

        private void BecomeFollower()
        {
            _node.BecomeFollower(CurrentState);
        }

        private void BecomeCandidate()
        {
            _node.BecomeCandidate(CurrentState);
        }
    }
}