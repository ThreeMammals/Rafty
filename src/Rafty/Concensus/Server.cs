using System;
using System.Collections.Generic;
using System.Threading;
using Rafty.Concensus;

namespace Rafty
{
    public class Server
    {
        public Server(Guid id)
        {
            Id = id;
        }
        
        public State State { get; private set; }
        public long CurrentTerm { get; private set; }
        public Guid VotedFor { get; private set; }
        public long CommitIndex { get; private set; }
        public long LastApplied { get; private set; }
        public Uri Address {get;private set;}
        public Guid Id {get;private set;}

        public void TimeOut()
        {
            State = State.Candidate;
        }

        public void BecomeLeader()
        {
            State = State.Leader;
        }

        public void Handle(RequestVoteResponse response)
        {
            
        }

        public void Handle(RequestVote response)
        {
            
        }
    }

    public enum State
    {
        Follower,
        Candidate,
        Leader
    }
}