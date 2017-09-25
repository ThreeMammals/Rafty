using System;
using Rafty.Concensus;

namespace Rafty.UnitTests
{
    public class NothingNode : INode
    {
        public IState State { get; }

        public int BecomeLeaderCount { get; private set; } 
        public int BecomeFollowerCount { get; private set; } 
        public int BecomeCandidateCount { get; private set; }

        public void BecomeLeader(CurrentState state)
        {
            BecomeLeaderCount++;
        }

        public void BecomeFollower(CurrentState state)
        {
            BecomeFollowerCount++;
        }

        public void BecomeCandidate(CurrentState state)
        {
            BecomeCandidateCount++;
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            return new AppendEntriesResponseBuilder().Build();
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {
            return new RequestVoteResponseBuilder().Build();
        }

        public void Start(Guid id)
        {
            throw new System.NotImplementedException();
        }

        public void Stop()
        {
            throw new System.NotImplementedException();
        }

        public Response<T> Accept<T>(T command)
        {
            throw new System.NotImplementedException();
        }
    }
}