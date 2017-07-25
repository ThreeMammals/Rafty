using System;
using System.Collections.Generic;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;
using Rafty.Log;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    public class AppendEntriesTests : IDisposable
    {
/*
1. Reply false if term < currentTerm (§5.1)
2. Reply false if log doesn’t contain an entry at prevLogIndex
whose term matches prevLogTerm (§5.3)
3. If an existing entry conflicts with a new one (same index
but different terms), delete the existing entry and all that
follow it (§5.3)
4. Append any new entries not already in the log
5. If leaderCommit > commitIndex, set commitIndex =
min(leaderCommit, index of last new entry)
*/

        private IFiniteStateMachine _fsm;
        private Node _node;
        private ISendToSelf _sendToSelf;
        private CurrentState _currentState;
        
        public AppendEntriesTests()
        {
            _fsm = new InMemoryStateMachine();
            _sendToSelf = new TestingSendToSelf();
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), 0, 0);
            _node = new Node(_currentState, _sendToSelf, _fsm);
            _sendToSelf.SetNode(_node);
        }
        

        public void Dispose()
        {
            _node.Dispose();
        }

        
        [Fact(DisplayName = "AppendEntries - 1. Reply false if term < currentTerm (§5.1)")]
        public void ShouldReplyFalseIfRpcTermLessThanCurrentTerm()
        {
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 1, default(Guid), TimeSpan.FromSeconds(5), new InMemoryLog(), 0, 0);
            _node = new Node(_currentState, _sendToSelf, _fsm);
            var appendEntriesRpc = new AppendEntriesBuilder().WithTerm(0).Build();
            var response = _node.Handle(appendEntriesRpc);
            response.Success.ShouldBe(false);
            response.Term.ShouldBe(1);
        }

        [Fact(DisplayName = "AppendEntries - 2. Reply false if log doesn’t contain an entry at prevLogIndex whose term matches prevLogTerm (§5.3)")]
        public void ShouldReplyFalseIfLogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesRpcPrevLogTerm()
        {
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 2, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), 0, 0);
            _currentState.Log.Apply(new LogEntry("", typeof(string), 2, 0));
            _node = new Node(_currentState, _sendToSelf, _fsm);
            var appendEntriesRpc = new AppendEntriesBuilder().WithTerm(2).WithPreviousLogIndex(0).WithPreviousLogTerm(1).Build();
            var response = _node.Handle(appendEntriesRpc);
            response.Success.ShouldBe(false);
            response.Term.ShouldBe(2);
        }

        [Fact(DisplayName = "AppendEntries - 3. If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it(§5.3)")]
        public void ShouldDeleteExistingEntryIfItConflictsWithNewOne()
        {
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 1, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), 2, 0);
            _currentState.Log.Apply(new LogEntry("term 1 commit index 0", typeof(string), 1, 0));
            _currentState.Log.Apply(new LogEntry("term 1 commit index 1", typeof(string), 1, 1));
            _currentState.Log.Apply(new LogEntry("term 1 commit index 2", typeof(string), 1, 2));
            _node = new Node(_currentState, _sendToSelf, _fsm);
            var appendEntriesRpc = new AppendEntriesBuilder()
                .WithEntry(new LogEntry("term 2 commit index 2", typeof(string),2,2))
                .WithTerm(2)
                .WithPreviousLogIndex(1)
                .WithPreviousLogTerm(1)
                .Build();
            var response = _node.Handle(appendEntriesRpc);
            response.Success.ShouldBe(true);
            response.Term.ShouldBe(2);
        }

        [Fact(DisplayName = "AppendEntries - 3. If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it(§5.3) and Append any new entries not already in the log")]
        public void ShouldDeleteExistingEntryIfItConflictsWithNewOneAndAppendNewEntries()
        {
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 1, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), 0, 0);
            _currentState.Log.Apply(new LogEntry("term 1 commit index 0", typeof(string), 1, 0));
            _currentState.Log.Apply(new LogEntry("term 1 commit index 1", typeof(string), 1, 1));
            _currentState.Log.Apply(new LogEntry("term 1 commit index 2", typeof(string), 1, 2));
            _node = new Node(_currentState, _sendToSelf, _fsm);
            var appendEntriesRpc = new AppendEntriesBuilder()
                .WithEntry(new LogEntry("term 2 commit index 2", typeof(string), 2, 2))
                .WithTerm(2)
                .WithPreviousLogIndex(1)
                .WithPreviousLogTerm(1)
                .Build();
            var response = _node.Handle(appendEntriesRpc);
            response.Success.ShouldBe(true);
            response.Term.ShouldBe(2);
            _node.State.CurrentState.Log.GetTermAtIndex(2).ShouldBe(2);
        }

        [Fact(DisplayName = "AppendEntries - 4. Append any new entries not already in the log")]
        public void ShouldAppendAnyEntriesNotInTheLog()
        {
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 1, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), 0, 0);
            _currentState.Log.Apply(new LogEntry("term 1 commit index 0", typeof(string), 1, 0));
            _node = new Node(_currentState, _sendToSelf, _fsm);
            var appendEntriesRpc = new AppendEntriesBuilder()
                .WithEntry(new LogEntry("term 1 commit index 1", typeof(string), 1, 1))
                .WithTerm(1)
                .WithPreviousLogIndex(0)
                .WithPreviousLogTerm(1)
                .Build();
            var response = _node.Handle(appendEntriesRpc);
            response.Success.ShouldBe(true);
            response.Term.ShouldBe(1);
            _node.State.CurrentState.Log.GetTermAtIndex(1).ShouldBe(1);
        }

        [Fact(DisplayName = "AppendEntries - Follower - 5. If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)")]
        public void FollowerShouldSetCommitIndexIfLeaderCommitGreaterThanCommitIndex()
        {
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 1, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), -1, -1);
            var log = new LogEntry("term 1 commit index 0", typeof(string), 1, 0);
            var appendEntriesRpc = new AppendEntriesBuilder()
               .WithEntry(log)
               .WithTerm(1)
               .WithPreviousLogIndex(-1)
               .WithPreviousLogTerm(0)
               .WithLeaderCommitIndex(0)
               .Build();
            //assume node has applied log..
            _currentState.Log.Apply(log);
            _sendToSelf = new TestingSendToSelf();
            var follower = new Follower(_currentState, _sendToSelf, _fsm);
            var state = follower.Handle(appendEntriesRpc);
            state.CurrentState.CommitIndex.ShouldBe(0);
        }

        [Fact(DisplayName = "AppendEntries - Candidate - 5. If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)")]
        public void CandidateShouldSetCommitIndexIfLeaderCommitGreaterThanCommitIndex()
        {
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), -1, -1);
            //assume log applied by node?
            var log = new LogEntry("term 1 commit index 0", typeof(string), 1, 0);
            _currentState.Log.Apply(log);
            var appendEntriesRpc = new AppendEntriesBuilder()
               .WithEntry(log)
               .WithTerm(1)
               .WithPreviousLogIndex(-1)
               .WithPreviousLogTerm(0)
               .WithLeaderCommitIndex(0)
               .Build();
            _sendToSelf = new TestingSendToSelf();
            var follower = new Candidate(_currentState, _sendToSelf, _fsm);
            var state = follower.Handle(appendEntriesRpc);
            state.CurrentState.CommitIndex.ShouldBe(0);
        }

        [Fact(DisplayName = "AppendEntries - Leader - 5. If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)")]
        public void LeaderShouldSetCommitIndexIfLeaderCommitGreaterThanCommitIndex()
        {
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 1, default(Guid), TimeSpan.FromSeconds(5), 
                new InMemoryLog(), -1, -1);
            //assume log applied by node?
            var log = new LogEntry("term 1 commit index 0", typeof(string), 1, 0);
            _currentState.Log.Apply(log);
            var appendEntriesRpc = new AppendEntriesBuilder()
               .WithEntry(log)
               .WithTerm(1)
               .WithPreviousLogIndex(-1)
               .WithPreviousLogTerm(0)
               .WithLeaderCommitIndex(0)
               .Build();
            _sendToSelf = new TestingSendToSelf();
            var follower = new Leader(_currentState, _sendToSelf, _fsm);
            var state = follower.Handle(appendEntriesRpc);
            state.CurrentState.CommitIndex.ShouldBe(0);
        }
    }
}