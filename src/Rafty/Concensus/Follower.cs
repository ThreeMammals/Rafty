using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Rafty.FiniteStateMachine;
using Rafty.Log;

namespace Rafty.Concensus
{
    public sealed class Follower : IState
    {
        private readonly IFiniteStateMachine _fsm;
        private readonly ILog _log;
        private readonly IRandomDelay _random;
        private Timer _electionTimer;
        private int _messagesSinceLastElectionExpiry;
        private readonly INode _node;
        private ISettings _settings;

        public Follower(CurrentState state, IFiniteStateMachine stateMachine, ILog log, IRandomDelay random, INode node, ISettings settings)
        {
            _random = random;
            _node = node;
            _settings = settings;
            _fsm = stateMachine;
            CurrentState = state;
            _log = log;
            ResetElectionTimer();
        }

        public CurrentState CurrentState { get; private set;}

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
            
            return new AppendEntriesResponse(CurrentState.CurrentTerm, true);
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {
            var response = RequestVoteTermIsGreaterThanCurrentTerm(requestVote);

            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            response = RequestVoteTermIsLessThanCurrentTerm(requestVote);

            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            response = VotedForIsNotThisOrNobody(requestVote);

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
            //todo - send message to leader...
            throw new NotImplementedException();
        }

        public void Stop()
        {
            _electionTimer.Dispose();
        }

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) RequestVoteTermIsGreaterThanCurrentTerm(RequestVote requestVote)
        {
            var term = CurrentState.CurrentTerm;

            if (requestVote.Term > CurrentState.CurrentTerm)
            {
                term = requestVote.Term;
                CurrentState = new CurrentState(CurrentState.Id, term, requestVote.CandidateId,
                    CurrentState.CommitIndex, CurrentState.LastApplied);
                 return (new RequestVoteResponse(true, CurrentState.CurrentTerm), true);
            }

            return (null, false);
        }

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) RequestVoteTermIsLessThanCurrentTerm(RequestVote requestVote)
        {
            if (requestVote.Term < CurrentState.CurrentTerm)
            {
                return (new RequestVoteResponse(false, CurrentState.CurrentTerm), false);
            }

            return (null, false);
        }

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) VotedForIsNotThisOrNobody(RequestVote requestVote)
        {
            if (CurrentState.VotedFor == CurrentState.Id || CurrentState.VotedFor != default(Guid))
            {
                return (new RequestVoteResponse(false, CurrentState.CurrentTerm), true);
            }

            return (null, false);
        }

        private (RequestVoteResponse requestVoteResponse, bool shouldReturn) LastLogIndexAndLastLogTermMatchesThis(RequestVote requestVote)
        {
             if (requestVote.LastLogIndex == _log.LastLogIndex &&
                requestVote.LastLogTerm == _log.LastLogTerm)
            {
                CurrentState = new CurrentState(CurrentState.Id, CurrentState.CurrentTerm, requestVote.CandidateId, CurrentState.CommitIndex, CurrentState.LastApplied);

                _messagesSinceLastElectionExpiry++;
                return (new RequestVoteResponse(true, CurrentState.CurrentTerm), true);
            }

            return (null, false);
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

        private void DeleteAnyConflictsInLog(AppendEntries appendEntries)
        {
            var count = 1;
            foreach (var newLog in appendEntries.Entries)
            {
                _log.DeleteConflictsFromThisLog(appendEntries.PreviousLogIndex + 1, newLog);
                count++;
            }
        }

        private void ApplyEntriesToLog(AppendEntries appendEntries)
        {
            foreach (var log in appendEntries.Entries)
            {
                _log.Apply(log);
            }
        }

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

        private void ApplyToStateMachine(int commitIndex, int lastApplied, AppendEntries appendEntries)
        {
            while (commitIndex > lastApplied)
            {
                lastApplied++;
                var log = _log.Get(lastApplied);
                _fsm.Handle(log.CommandData);
            }

            CurrentState = new CurrentState(CurrentState.Id, appendEntries.Term,
                CurrentState.VotedFor, commitIndex, lastApplied);

            _messagesSinceLastElectionExpiry++;
        }
        
        private void ElectionTimerExpired()
        {
            if (_messagesSinceLastElectionExpiry == 0)
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
            _messagesSinceLastElectionExpiry = 0;
            var timeout = _random.Get(_settings.MinTimeout, _settings.MaxTimeout);
            _electionTimer?.Dispose();
            _electionTimer = new Timer(x =>
            {
                ElectionTimerExpired();

            }, null, Convert.ToInt32(timeout.TotalMilliseconds), Convert.ToInt32(timeout.TotalMilliseconds));
        }
    }
}