using System;
using Rafty.Messages;

namespace Rafty.Responses
{
    public class AppendEntriesResponse : Message
    {

        public AppendEntriesResponse(int term, bool success, Guid followerId, Guid leaderId)
        {
            this.Term = term;
            this.Success = success;
            this.FollowerId = followerId;
            LeaderId = leaderId;
        }
        
        public int Term { get; private set; }
        public bool Success { get; private set; }
        public Guid FollowerId { get; private set; }
        public Guid LeaderId { get; private set; }
    }
}