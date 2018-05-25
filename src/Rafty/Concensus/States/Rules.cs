using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rafty.Log;

namespace Rafty.Concensus.States
{
    public interface IRules 
    {
        (AppendEntriesResponse appendEntriesResponse, bool shouldReturn) AppendEntriesTermIsLessThanCurrentTerm(AppendEntries appendEntries, CurrentState currentState);
        Task<(AppendEntriesResponse appendEntriesResponse, bool shouldReturn)> LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(AppendEntries appendEntries, ILog log, CurrentState currentState);
        Task DeleteAnyConflictsInLog(AppendEntries appendEntries, ILog log, ILogger logger, string id);
        Task ApplyNewEntriesToLog(AppendEntries appendEntries, ILog log, ILogger logger, string id);
        Task<(int commitIndex, int lastApplied)> CommitIndexAndLastApplied(AppendEntries appendEntries, ILog log, CurrentState currentState);
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
        public async Task<(int commitIndex, int lastApplied)> CommitIndexAndLastApplied(AppendEntries appendEntries, ILog log, CurrentState currentState)
        {
            var commitIndex = currentState.CommitIndex;
            var lastApplied = currentState.LastApplied;
            if (appendEntries.LeaderCommitIndex > currentState.CommitIndex)
            {
                var lastNewEntry = await log.LastLogIndex();
                commitIndex = System.Math.Min(appendEntries.LeaderCommitIndex, lastNewEntry);
            }

            return (commitIndex, lastApplied);
        }
        // todo - inject as function into candidate and follower as logic is the same...
        public async Task ApplyNewEntriesToLog(AppendEntries appendEntries, ILog log, ILogger logger, string id)
        {
            foreach (var entry in appendEntries.Entries)
            {
                var index = appendEntries.PreviousLogIndex;

                var duplicate = await log.IsDuplicate(index, entry);

                if(duplicate)
                {
                    logger.LogInformation($"id:{id} had dup log, index:{index}, term:{entry.Term}");
                }

                if(!duplicate)
                {
                    await log.Apply(entry);
                }
            }
        }

         // todo - inject as function into candidate and follower as logic is the same...
        public async Task DeleteAnyConflictsInLog(AppendEntries appendEntries, ILog log, ILogger logger, string id)
        {
            foreach (var newLog in appendEntries.Entries)
            {
                var index = appendEntries.PreviousLogIndex;
                logger.LogInformation($"{id} Deleting index: {index}, appendEntries.PreviousLogIndex: {appendEntries.PreviousLogIndex}");
                await log.DeleteConflictsFromThisLog(index, newLog, logger, id);
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
        public async Task<(AppendEntriesResponse appendEntriesResponse, bool shouldReturn)> LogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesPreviousLogTerm(AppendEntries appendEntries, ILog log, CurrentState currentState)
        {
            var termAtPreviousLogIndex = await log.GetTermAtIndex(appendEntries.PreviousLogIndex);
            if (termAtPreviousLogIndex > 0 && termAtPreviousLogIndex != appendEntries.PreviousLogTerm)
            {
                return (new AppendEntriesResponse(currentState.CurrentTerm, false), true);
            }

            return (null, false);
        }
    }
}