namespace Rafty.UnitTests
{
    using System;
    using Concensus;
    /* Followers(�5.2):
    � Respond to RPCs from candidates and leaders
    � If election timeout elapses without receiving AppendEntries
    RPC from current leader or granting vote to candidate:
    convert to candidate
    */

    public class TestingNode : INode
    {
        
        public IState State { get; private set; }

        public int BecomeCandidateCount { get; private set; }

        public void SetState(IState state)
        {
            State = state;
        }

        public void BecomeLeader(CurrentState state)
        {
            throw new NotImplementedException();
        }

        public void BecomeFollower(CurrentState state)
        {
            throw new NotImplementedException();
        }

        public void BecomeCandidate(CurrentState state)
        {
            BecomeCandidateCount++;
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            return State.Handle(appendEntries);
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {
            return State.Handle(requestVote);
        }

        public void Start(Guid id)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public Response<T> Accept<T>(T command)
        {
            throw new NotImplementedException();
        }
    }
}