using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private bool WonElection()
        {
            return _becomeLeader && !_requestVoteResponseWithGreaterTerm;
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
            // todo - work out what a candidate should do if it gets a command sent to it? Maybe return a retry?
            throw new NotImplementedException();
        }

        public void Stop()
        {
            _electionTimer.Dispose();
        }


        private List<Task> GetVotes(BlockingCollection<RequestVoteResponse> responses)
        {
            var tasks = new List<Task>();

            foreach (var peer in _peers)
            {
                var task = GetVote(peer, responses);
                tasks.Add(task);
            }

            return tasks;
        }

        private async Task CountVotes(BlockingCollection<RequestVoteResponse> states)
        {
            var receivedResponses = 0;
            var tasks = new List<Task>();
            foreach (var requestVoteResponse in states.GetConsumingEnumerable())
            {
                var task = CountVote(requestVoteResponse);
                tasks.Add(task);
                receivedResponses++;

                if (receivedResponses >= _peers.Count)
                {
                    break;
                }
            }

            Task.WaitAll(tasks.ToArray());
        }

        private async Task CountVote(RequestVoteResponse requestVoteResponse)
        {
            if (requestVoteResponse.Term > CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, requestVoteResponse.Term,
                    CurrentState.VotedFor, CurrentState.CommitIndex, CurrentState.LastApplied);

                _requestVoteResponseWithGreaterTerm = true;
            }

            if (requestVoteResponse.VoteGranted)
            {
                lock (_lock)
                {
                    _votesThisElection++;

                    //If votes received from majority of servers: become leader
                    if (_votesThisElection >= (_peers.Count + 1) / 2 + 1)
                    {
                        _becomeLeader = true;
                    }
                }
            }
        }

        private async Task GetVote(IPeer peer, BlockingCollection<RequestVoteResponse> responses) 
        {
            var requestVoteResponse = peer.Request(new RequestVote(CurrentState.CurrentTerm, CurrentState.Id, _log.LastLogIndex, _log.LastLogTerm));

            responses.Add(requestVoteResponse);
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
                CurrentState.VotedFor, commitIndex, lastApplied);
        }

        private void AppendEntriesTermIsGreaterThanCurrentTerm(AppendEntries appendEntries)
        {
            if (appendEntries.Term > CurrentState.CurrentTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, appendEntries.Term, CurrentState.VotedFor,
                    CurrentState.CommitIndex, CurrentState.LastApplied);

                _node.BecomeFollower(CurrentState);
            }
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

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) LastLogIndexAndLastLogTermMatchesThis(RequestVote requestVote)
        {
             if (requestVote.LastLogIndex == _log.LastLogIndex &&
                requestVote.LastLogTerm == _log.LastLogTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, requestVote.CandidateId, CurrentState.CommitIndex, CurrentState.LastApplied);

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
                CurrentState.CommitIndex, CurrentState.LastApplied);
        }
        
        private void DoElection()
        {
            var requestVoteResponses = new BlockingCollection<RequestVoteResponse>();
            
            var votes = GetVotes(requestVoteResponses);

            var checkVotes = CountVotes(requestVoteResponses);

            Task.WaitAll(votes.ToArray());

            checkVotes.Wait();
        }
    }
}