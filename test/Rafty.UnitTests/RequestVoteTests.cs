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
        private Node _node;
        private ISendToSelf _sendToSelf;
        private CurrentState _currentState;
        private IFiniteStateMachine _fsm;
        private List<IPeer> _peers;
        private ILog _log;
        private IRandomDelay _random;

        public RequestVoteTests()
        {
            _random = new RandomDelay();
            _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _sendToSelf = new TestingSendToSelf();
            _currentState = new CurrentState(Guid.NewGuid(), 0, default(Guid), 
                TimeSpan.FromSeconds(5), 0, 0);
            _node = new Node(_sendToSelf, _fsm, _log, _random);
            _sendToSelf.SetNode(_node);
        }

        public void Dispose()
        {
            _node.Dispose();
        }

        [Fact(DisplayName = "RequestVote - 1. Reply false if term<currentTerm (§5.1)")]
        public void ShouldReplyFalseIfTermIsLessThanCurrentTerm()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), 
                TimeSpan.FromSeconds(5), 1, 0);
            _node = new Node(_sendToSelf, _fsm, _log, _random);
            var requestVoteRpc = new RequestVoteBuilder().WithTerm(0).Build();
            var response = _node.Handle(requestVoteRpc);
            response.VoteGranted.ShouldBe(false);
            response.Term.ShouldBe(1);
        }

        [Fact(DisplayName = "RequestVote - 2. Reply false if voted for is not default")]
        public void ShouldReplyFalseIfVotedForIsNotDefault()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, Guid.NewGuid(), 
                TimeSpan.FromSeconds(5), 1, 0);
            _node = new Node(_sendToSelf, _fsm, _log, _random);
            var requestVoteRpc = new RequestVoteBuilder().WithTerm(0).Build();
            var response = _node.Handle(requestVoteRpc);
            response.VoteGranted.ShouldBe(false);
            response.Term.ShouldBe(1);
        }

        [Fact(DisplayName = "RequestVote - 2. Reply false if voted for is not candidateId")]
        public void ShouldReplyFalseIfVotedForIsNotCandidateId()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, Guid.NewGuid(), 
                TimeSpan.FromSeconds(5), 1, 0);
            _node = new Node(_sendToSelf, _fsm, _log, _random);
            var requestVoteRpc = new RequestVoteBuilder().WithCandidateId(Guid.NewGuid()).WithTerm(0).Build();
            var response = _node.Handle(requestVoteRpc);
            response.VoteGranted.ShouldBe(false);
            response.Term.ShouldBe(1);
        }

        [Fact(DisplayName = "RequestVote - 2. If votedFor is null or candidateId, and candidate’s log is atleast as up - to - date as receiver’s log, grant vote(§5.2, §5.4)")]
        public void ShouldGrantVote()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), TimeSpan.FromSeconds(5), 1, 0);
            _node = new Node(_sendToSelf, _fsm, _log, _random);
            var requestVoteRpc = new RequestVoteBuilder().WithLastLogIndex(0).WithLastLogTerm(0).WithTerm(1).Build();
            var response = _node.Handle(requestVoteRpc);
            response.VoteGranted.ShouldBe(true);
            response.Term.ShouldBe(1);
        }
    }
}
