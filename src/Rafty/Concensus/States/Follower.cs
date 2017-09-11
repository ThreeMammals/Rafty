using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Rafty.Concensus.States;
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
        private IRules _rules;
        private List<IPeer> _peers;

        public Follower(
            CurrentState state, 
            IFiniteStateMachine stateMachine, 
            ILog log, 
            IRandomDelay random, 
            INode node, 
            ISettings settings, 
            IRules rules,
            List<IPeer> peers)
        {
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
            
            _messagesSinceLastElectionExpiry++;
            
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

            _messagesSinceLastElectionExpiry++;
            
            if(response.shouldReturn)
            {
                return response.requestVoteResponse;
            }

            return new RequestVoteResponse(false, CurrentState.CurrentTerm);
        }

        public Response<T> Accept<T>(T command)
        {
            var leader = _peers.FirstOrDefault(x => x.Id == CurrentState.LeaderId);
            if(leader != null)
            {
                return leader.Request(command);
            }
            
            return new Response<T>(false, command);
        }

        public void Stop()
        {
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

        private void ApplyToStateMachine(int commitIndex, int lastApplied, AppendEntries appendEntries)
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