using System;
using System.Collections.Generic;
using System.Text;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;
using Rafty.Log;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    public class RequestVoteTests : IDisposable
    {
/*
1. Reply false if term<currentTerm (§5.1)
2. If votedFor is null or candidateId, and candidate’s log is at
least as up-to-date as receiver’s log, grant vote(§5.2, §5.4)
*/
        private readonly INode _node;
        private CurrentState _currentState;
        private IFiniteStateMachine _fsm;
        private List<IPeer> _peers;
        private readonly ILog _log;
        private readonly IRandomDelay _random;

        public RequestVoteTests()
        {
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _node = new NothingNode();
        }

        public void Dispose()
        {
            //_node.Dispose();
        }

        [Fact(DisplayName = "RequestVote - 1. Reply false if term<currentTerm (§5.1)")]
        public void ShouldReplyFalseIfTermIsLessThanCurrentTerm()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), 1, 0, 100, 350);
            var requestVoteRpc = new RequestVoteBuilder().WithTerm(0).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node);
            var requestVoteResponse = follower.Handle(requestVoteRpc);
            requestVoteResponse.VoteGranted.ShouldBe(false);
            requestVoteResponse.Term.ShouldBe(1);
        }

        [Fact(DisplayName = "RequestVote - 2. Reply false if voted for is not default")]
        public void ShouldReplyFalseIfVotedForIsNotDefault()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, Guid.NewGuid(), 1, 0, 100, 350);
            var requestVoteRpc = new RequestVoteBuilder().WithTerm(0).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node);
            var requestVoteResponse = follower.Handle(requestVoteRpc);
            requestVoteResponse.VoteGranted.ShouldBe(false);
            requestVoteResponse.Term.ShouldBe(1);
        }

        [Fact(DisplayName = "RequestVote - 2. Reply false if voted for is not candidateId")]
        public void ShouldReplyFalseIfVotedForIsNotCandidateId()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, Guid.NewGuid(), 1, 0, 100, 350);
            var requestVoteRpc = new RequestVoteBuilder().WithCandidateId(Guid.NewGuid()).WithTerm(0).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node);
            var requestVoteResponse = follower.Handle(requestVoteRpc);
            requestVoteResponse.VoteGranted.ShouldBe(false);
            requestVoteResponse.Term.ShouldBe(1);
        }

        [Fact(DisplayName = "RequestVote - 2. If votedFor is null or candidateId, and candidate’s log is atleast as up - to - date as receiver’s log, grant vote(§5.2, §5.4)")]
        public void ShouldGrantVote()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), 1, 0, 100, 350);
            var requestVoteRpc = new RequestVoteBuilder().WithLastLogIndex(0).WithLastLogTerm(0).WithTerm(1).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node);
            var requestVoteResponse = follower.Handle(requestVoteRpc);
            requestVoteResponse.VoteGranted.ShouldBe(true);
            requestVoteResponse.Term.ShouldBe(1);
        }
    }
}
