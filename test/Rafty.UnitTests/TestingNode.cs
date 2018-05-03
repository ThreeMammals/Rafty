using Rafty.FiniteStateMachine;

namespace Rafty.UnitTests
{
    using System;
    using System.Threading.Tasks;
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

        public async Task<AppendEntriesResponse> Handle(AppendEntries appendEntries)
        {
            return await State.Handle(appendEntries);
        }

        public async Task<RequestVoteResponse> Handle(RequestVote requestVote)
        {
            return await State.Handle(requestVote);
        }

        public void Start(string id)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public async Task<Response<T>> Accept<T>(T command) where T : ICommand
        {
            throw new NotImplementedException();
        }
    }
}