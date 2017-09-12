using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rafty.Concensus.States;
using Rafty.FiniteStateMachine;
using Rafty.Log;

namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        private int _votesThisElection;
        private readonly object _lock = new object();
        private readonly IFiniteStateMachine _fsm;
        private readonly List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _random;
        private bool _becomeLeader;
        private bool _electioneering;
        private readonly INode _node;
        private Timer _electionTimer;
        private bool _requestVoteResponseWithGreaterTerm;
        private ISettings _settings;
        private IRules _rules;

        public Candidate(
            CurrentState currentState, 
            IFiniteStateMachine fsm, 
            List<IPeer> peers, 
            ILog log, 
            IRandomDelay random, 
            INode node, 
            ISettings settings,
            IRules rules)
        {
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
                _node.BecomeLeader(CurrentState);
                return;
            }

            DoElection();

            StopElectioneering();

            if (WonElection())
            {
                _node.BecomeLeader(CurrentState);
                return;
            }

            _node.BecomeFollower(CurrentState);
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            var response = _rules.AppendEntriesTermIsLessThanCurrentTerm(appendEntries, CurrentState);

            if(response.shouldReturn)
            {
                return response.appendEntriesResponse;
            }

            response = _rules.LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(appendEntries, _log, CurrentState);

            if(response.shouldReturn)
            {
                return response.appendEntriesResponse;
            }
          
            _rules.DeleteAnyConflictsInLog(appendEntries, _log);

            _rules.ApplyEntriesToLog(appendEntries, _log);

            var commitIndexAndLastApplied = _rules.CommitIndexAndLastApplied(appendEntries, _log, CurrentState);

            ApplyToStateMachine(commitIndexAndLastApplied.commitIndex, commitIndexAndLastApplied.lastApplied, appendEntries);

            SetLeaderId(appendEntries);
            
            AppendEntriesTermIsGreaterThanCurrentTerm(appendEntries);

            return new AppendEntriesResponse(CurrentState.CurrentTerm, true);
        }
        
        public RequestVoteResponse Handle(RequestVote requestVote)
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

            response = LastLogIndexAndLastLogTermMatchesThis(requestVote);

            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            return new RequestVoteResponse(false, CurrentState.CurrentTerm);
        }

        public Response<T> Accept<T>(T command)
        {
           return new Response<T>("Please retry command later. Currently electing new a new leader.", command);
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

            var votingCount = new List<Task>();

            foreach (var requestVoteResponse in requestVoteResponses.GetConsumingEnumerable())
            {
                var countedVote = CountVote(requestVoteResponse);

                votingCount.Add(countedVote);

                receivedResponses++;

                if (ReceivedMaximumResponses(receivedResponses))
                {
                    break;
                }
            }

            Task.WaitAll(votingCount.ToArray());
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
                        BecomeLeader();
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

        private void BecomeLeader()
        {
            _becomeLeader = true;
        }

        private async Task RequestVote(IPeer peer, BlockingCollection<RequestVoteResponse> requestVoteResponses) 
        {
            var requestVoteResponse = peer.Request(new RequestVote(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex, _log.LastLogTerm));

            requestVoteResponses.Add(requestVoteResponse);
        }

        private void ElectionTimerExpired()
        {
            if (NotElecting())
            {
                _node.BecomeCandidate(CurrentState);
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
                ElectionTimerExpired();

            }, null, Convert.ToInt32(timeout.TotalMilliseconds), Convert.ToInt32(timeout.TotalMilliseconds));
        }
        
        private void ApplyToStateMachine(int commitIndex, int lastApplied, AppendEntries appendEntries)
        {
            while (commitIndex > lastApplied)
            {
                lastApplied++;
                var log = _log.Get(lastApplied);
                _fsm.Handle(log.CommandData);
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

                _node.BecomeFollower(CurrentState);
            }
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

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) LastLogIndexAndLastLogTermMatchesThis(RequestVote requestVote)
        {
             if (requestVote.LastLogIndex == _log.LastLogIndex &&
                requestVote.LastLogTerm == _log.LastLogTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, requestVote.CandidateId, CurrentState.CommitIndex, CurrentState.LastApplied, CurrentState.LeaderId);

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

            var checkVotes = CountVotes(requestVoteResponses);

            Task.WaitAll(votes.ToArray());

            checkVotes.Wait();
        }

        private bool WonElection()
        {
            return _becomeLeader && !_requestVoteResponseWithGreaterTerm;
        }
    }
}