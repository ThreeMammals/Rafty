using System;
using Rafty.Log;

namespace Rafty.Concensus.States
{
    public interface IRules 
    {
        (AppendEntriesResponse appendEntriesResponse, bool shouldReturn) AppendEntriesTermIsLessThanCurrentTerm(AppendEntries appendEntries, CurrentState currentState);
        (AppendEntriesResponse appendEntriesResponse, bool shouldReturn) LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(AppendEntries appendEntries, ILog log, CurrentState currentState);
        void DeleteAnyConflictsInLog(AppendEntries appendEntries, ILog log);
        void ApplyEntriesToLog(AppendEntries appendEntries, ILog log);
        (int commitIndex, int lastApplied) CommitIndexAndLastApplied(AppendEntries appendEntries, ILog log, CurrentState currentState);
        (RequestVoteResponse requestVoteResponse, bool shouldReturn) RequestVoteTermIsLessThanCurrentTerm(RequestVote requestVote, CurrentState currentState);
        (RequestVoteResponse requestVoteResponse, bool shouldReturn) VotedForIsNotThisOrNobody(RequestVote requestVote, CurrentState currentState);
    }

    public class Rules : IRules
    {
        // todo - consolidate with candidate and pass in as function
        public (RequestVoteResponse requestVoteResponse, bool shouldReturn) VotedForIsNotThisOrNobody(RequestVote requestVote, CurrentState currentState)
        {
            if (currentState.VotedFor == currentState.Id || currentState.VotedFor != default(string))
            {
                return (new RequestVoteResponse(false, currentState.CurrentTerm), true);
            }

            return (null, false);
        }

        // todo - consolidate with candidate and pass in as function
        public (RequestVoteResponse requestVoteResponse, bool shouldReturn) RequestVoteTermIsLessThanCurrentTerm(RequestVote requestVote, CurrentState currentState)
        {
            if (requestVote.Term < currentState.CurrentTerm)
            {
                return (new RequestVoteResponse(false, currentState.CurrentTerm), false);
            }

            return (null, false);
        }

        // todo - inject as function into candidate and follower as logic is the same...
        public (int commitIndex, int lastApplied) CommitIndexAndLastApplied(AppendEntries appendEntries, ILog log, CurrentState currentState)
        {
            var commitIndex = currentState.CommitIndex;
            var lastApplied = currentState.LastApplied;
            if (appendEntries.LeaderCommitIndex > currentState.CommitIndex)
            {
                var lastNewEntry = log.LastLogIndex;
                commitIndex = System.Math.Min(appendEntries.LeaderCommitIndex, lastNewEntry);
            }

            return (commitIndex, lastApplied);
        }
        // todo - inject as function into candidate and follower as logic is the same...
        public void ApplyEntriesToLog(AppendEntries appendEntries, ILog log)
        {
            foreach (var entry in appendEntries.Entries)
            {
                log.Apply(entry);
            }
        }

         // todo - inject as function into candidate and follower as logic is the same...
        public void DeleteAnyConflictsInLog(AppendEntries appendEntries, ILog log)
        {
            var count = 1;
            foreach (var newLog in appendEntries.Entries)
            {
                log.DeleteConflictsFromThisLog(appendEntries.PreviousLogIndex + 1, newLog);
                count++;
            }
        }

        // todo - inject as function into candidate and follower as logic is the same...
        public (AppendEntriesResponse appendEntriesResponse, bool shouldReturn) AppendEntriesTermIsLessThanCurrentTerm(AppendEntries appendEntries, CurrentState currentState)
        {
            if (appendEntries.Term < currentState.CurrentTerm)
            { 
                return (new AppendEntriesResponse(currentState.CurrentTerm, false), true);
            }

            return (null, false);
        }

            // todo - inject as function into candidate and follower as logic is the same...
        public (AppendEntriesResponse appendEntriesResponse, bool shouldReturn) LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(AppendEntries appendEntries, ILog log, CurrentState currentState)
        {
            var termAtPreviousLogIndex = log.GetTermAtIndex(appendEntries.PreviousLogIndex);
            if (termAtPreviousLogIndex > 0 && termAtPreviousLogIndex != appendEntries.PreviousLogTerm)
            {
                return (new AppendEntriesResponse(currentState.CurrentTerm, false), true);
            }

            return (null, false);
        }
    }
}