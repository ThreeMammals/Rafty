using System;
using Rafty.State;

namespace Rafty.Messages
{
    public class AppendEntries : Message
    {

        public AppendEntries(int term, Guid leaderId, int previousLogIndex, int previousLogTerm, Log entry, int leaderCommit, Guid followerId)
        {
            Entry = entry;
            Term = term;
            LeaderId = leaderId;
            PreviousLogIndex = previousLogIndex;
            PreviousLogTerm = previousLogTerm;
            LeaderCommit = leaderCommit;
            FollowerId = followerId;
        }
        public int Term { get; private set; }
        public Guid LeaderId { get; private set; }
        public int PreviousLogIndex { get; private set; }
        public int PreviousLogTerm { get; private set; }
        public Log Entry { get; private set; }
        public int LeaderCommit { get; private set; }
        public Guid FollowerId { get; private set; }
    }
}