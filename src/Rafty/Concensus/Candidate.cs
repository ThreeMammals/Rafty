using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

        public Candidate(CurrentState currentState, 
            IFiniteStateMachine fsm, 
            List<IPeer> peers, 
            ILog log, 
            IRandomDelay random, 
            INode node, 
            ISettings settings)
        {
            _random = random;
            _node = node;
            _settings = settings;
            _log = log;
            _peers = peers;
            _fsm = fsm;
            CurrentState = currentState;
            _electioneering = true;
            ResetElectionTimer();
        }

        public CurrentState CurrentState { get; private set;}

        public void BeginElection()
        {
            var nextTerm = CurrentState.CurrentTerm + 1;

            var votedFor = CurrentState.Id;

            _votesThisElection++;

            CurrentState = new CurrentState(CurrentState.Id, nextTerm, votedFor, 
                CurrentState.CommitIndex, CurrentState.LastApplied);

            var responses = new BlockingCollection<RequestVoteResponse>();
            
            if (_peers.Count == 0)
            {
                _electioneering = false;
                _node.BecomeLeader(CurrentState);
                return;
            }

            var votes = GetVotes(responses);

            var checkVotes = CountVotes(responses);

            Task.WaitAll(votes.ToArray());

            checkVotes.Wait();

            _electioneering = false;

            if (_becomeLeader && !_requestVoteResponseWithGreaterTerm)
            {
                _node.BecomeLeader(CurrentState);
                return;
            }

            _node.BecomeFollower(CurrentState);
        }

        // todo - inject as function into candidate and follower as logic is the same...
        private (AppendEntriesResponse appendEntriesResponse, bool shouldReturn) AppendEntriesTermIsLessThanCurrentTerm(AppendEntries appendEntries)
        {
            if (appendEntries.Term < CurrentState.CurrentTerm)
            { 
                return (new AppendEntriesResponse(CurrentState.CurrentTerm, false), true);
            }

            return (null, false);
        }

        // todo - inject as function into candidate and follower as logic is the same...
        private (AppendEntriesResponse appendEntriesResponse, bool shouldReturn) LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(AppendEntries appendEntries)
        {
            var termAtPreviousLogIndex = _log.GetTermAtIndex(appendEntries.PreviousLogIndex);
            if (termAtPreviousLogIndex > 0 && termAtPreviousLogIndex != appendEntries.PreviousLogTerm)
            {
                return (new AppendEntriesResponse(CurrentState.CurrentTerm, false), true);
            }

            return (null, false);
        }
        
        // todo - inject as function into candidate and follower as logic is the same...
        private void DeleteAnyConflictsInLog(AppendEntries appendEntries)
        {
            var count = 1;
            foreach (var newLog in appendEntries.Entries)
            {
                _log.DeleteConflictsFromThisLog(appendEntries.PreviousLogIndex + 1, newLog);
                count++;
            }
        }

        // todo - inject as function into candidate and follower as logic is the same...
        private void ApplyEntriesToLog(AppendEntries appendEntries)
        {
            foreach (var log in appendEntries.Entries)
            {
                _log.Apply(log);
            }
        }

        // todo - inject as function into candidate and follower as logic is the same...
        private (int commitIndex, int lastApplied) CommitIndexAndLastApplied(AppendEntries appendEntries)
        {
            var commitIndex = CurrentState.CommitIndex;
            var lastApplied = CurrentState.LastApplied;
            if (appendEntries.LeaderCommitIndex > CurrentState.CommitIndex)
            {
                var lastNewEntry = _log.LastLogIndex;
                commitIndex = System.Math.Min(appendEntries.LeaderCommitIndex, lastNewEntry);
            }

            return (commitIndex, lastApplied);
        }

        // not the same as follower
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

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            var response = AppendEntriesTermIsLessThanCurrentTerm(appendEntries);

            if(response.shouldReturn)
            {
                return response.appendEntriesResponse;
            }

            response = LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(appendEntries);

            if(response.shouldReturn)
            {
                return response.appendEntriesResponse;
            }
          
            DeleteAnyConflictsInLog(appendEntries);

            ApplyEntriesToLog(appendEntries);

            var commitIndexAndLastApplied = CommitIndexAndLastApplied(appendEntries);

            ApplyToStateMachine(commitIndexAndLastApplied.commitIndex, commitIndexAndLastApplied.lastApplied, appendEntries);

            AppendEntriesTermIsGreaterThanCurrentTerm(appendEntries);

            return new AppendEntriesResponse(CurrentState.CurrentTerm, true);
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {
            var term = CurrentState.CurrentTerm;

            //If RPC request or response contains term T > currentTerm: set currentTerm = T, convert to follower (§5.1)
            if (requestVote.Term > CurrentState.CurrentTerm)
            {
                term = requestVote.Term;
                // update voted for....
                CurrentState = new CurrentState(CurrentState.Id, term, requestVote.CandidateId,
                    CurrentState.CommitIndex, CurrentState.LastApplied);
                _node.BecomeFollower(CurrentState);
                return new RequestVoteResponse(true, CurrentState.CurrentTerm);
            }

            //Reply false if term<currentTerm
            if (requestVote.Term < CurrentState.CurrentTerm)
            {
                return new RequestVoteResponse(false, CurrentState.CurrentTerm);
            }

            //Reply false if voted for is not candidateId
            //Reply false if voted for is not default
            if (CurrentState.VotedFor == CurrentState.Id || CurrentState.VotedFor != default(Guid))
            {
                return new RequestVoteResponse(false, CurrentState.CurrentTerm);
            }

            if (requestVote.LastLogIndex == _log.LastLogIndex &&
                requestVote.LastLogTerm == _log.LastLogTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm,
                    requestVote.CandidateId, CurrentState.CommitIndex, CurrentState.LastApplied);

                //candidate cannot vote for anyone else...
                return new RequestVoteResponse(true, CurrentState.CurrentTerm);
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
            if (!_electioneering)
            {
                _node.BecomeCandidate(CurrentState);
            }
            else
            {
                ResetElectionTimer();
            }
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
    }
}