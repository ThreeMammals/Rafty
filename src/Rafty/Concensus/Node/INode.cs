using System;
using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    public interface INode
    {
        IState State { get; }
        void BecomeLeader(CurrentState state);
        void BecomeFollower(CurrentState state);
        void BecomeCandidate(CurrentState state);
        AppendEntriesResponse Handle(AppendEntries appendEntries);
        RequestVoteResponse Handle(RequestVote requestVote);
        void Start(string id);
        void Stop();
        Response<T> Accept<T>(T command) where T : ICommand;
    }
}