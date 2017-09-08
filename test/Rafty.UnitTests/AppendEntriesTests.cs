using System;
using System.Collections.Generic;
using Rafty.Concensus;
using Rafty.Concensus.States;
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

        private readonly IFiniteStateMachine _fsm;
        private readonly INode _node;
        private CurrentState _currentState;
        private readonly ILog _log;
        private List<IPeer> _peers;
        private readonly IRandomDelay _random;
        private Settings _settings;
        private IRules _rules;


        public AppendEntriesTests()
        {
            _rules = new Rules();
            _settings = new SettingsBuilder().Build();
            _random = new RandomDelay();
             _log = new InMemoryLog();
            _peers = new List<IPeer>();
            _fsm = new InMemoryStateMachine();
            _node = new NothingNode();
        }
        

        public void Dispose()
        {
            //_node.Dispose();
        }

        
        [Fact(DisplayName = "AppendEntries - 1. Reply false if term < currentTerm (§5.1)")]
        public void ShouldReplyFalseIfRpcTermLessThanCurrentTerm()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), 0, 0);
            var appendEntriesRpc = new AppendEntriesBuilder().WithTerm(0).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = follower.Handle(appendEntriesRpc);
            appendEntriesResponse.Success.ShouldBe(false);
            appendEntriesResponse.Term.ShouldBe(1);
        }

        [Fact(DisplayName = "AppendEntries - 2. Reply false if log doesn’t contain an entry at prevLogIndex whose term matches prevLogTerm (§5.3)")]
        public void ShouldReplyFalseIfLogDoesntContainEntryAtPreviousLogIndexWhoseTermMatchesRpcPrevLogTerm()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 2, default(Guid), 0, 0);
            _log.Apply(new LogEntry("", typeof(string), 2));
            var appendEntriesRpc = new AppendEntriesBuilder().WithTerm(2).WithPreviousLogIndex(1).WithPreviousLogTerm(1).Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = follower.Handle(appendEntriesRpc);
            appendEntriesResponse.Success.ShouldBe(false);
            appendEntriesResponse.Term.ShouldBe(2);
        }

        [Fact(DisplayName = "AppendEntries - 3. If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it(§5.3)")]
        public void ShouldDeleteExistingEntryIfItConflictsWithNewOne()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), 2, 0);
            _log.Apply(new LogEntry("term 1 commit index 0", typeof(string), 1));
            _log.Apply(new LogEntry("term 1 commit index 1", typeof(string), 1));
            _log.Apply(new LogEntry("term 1 commit index 2", typeof(string), 1));
            var appendEntriesRpc = new AppendEntriesBuilder()
                .WithEntry(new LogEntry("term 2 commit index 2", typeof(string),2))
                .WithTerm(2)
                .WithPreviousLogIndex(1)
                .WithPreviousLogTerm(1)
                .Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = follower.Handle(appendEntriesRpc);
            appendEntriesResponse.Success.ShouldBe(true);
            appendEntriesResponse.Term.ShouldBe(2);
        }

        [Fact(DisplayName = "AppendEntries - 3. If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it(§5.3) and Append any new entries not already in the log")]
        public void ShouldDeleteExistingEntryIfItConflictsWithNewOneAndAppendNewEntries()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), 0, 0);
            _log.Apply(new LogEntry("term 1 commit index 0", typeof(string), 1));
            _log.Apply(new LogEntry("term 1 commit index 1", typeof(string), 1));
            _log.Apply(new LogEntry("term 1 commit index 2", typeof(string), 1));
            var appendEntriesRpc = new AppendEntriesBuilder()
                .WithEntry(new LogEntry("term 2 commit index 2", typeof(string), 2))
                .WithTerm(2)
                .WithPreviousLogIndex(1)
                .WithPreviousLogTerm(1)
                .Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = follower.Handle(appendEntriesRpc);
            appendEntriesResponse.Success.ShouldBe(true);
            appendEntriesResponse.Term.ShouldBe(2);
            _log.GetTermAtIndex(2).ShouldBe(2);
        }

        [Fact(DisplayName = "AppendEntries - 4. Append any new entries not already in the log")]
        public void ShouldAppendAnyEntriesNotInTheLog()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), 0, 0);
            _log.Apply(new LogEntry("term 1 commit index 0", typeof(string), 1));
            var appendEntriesRpc = new AppendEntriesBuilder()
                .WithEntry(new LogEntry("term 1 commit index 1", typeof(string), 1))
                .WithTerm(1)
                .WithPreviousLogIndex(1)
                .WithPreviousLogTerm(1)
                .Build();
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = follower.Handle(appendEntriesRpc);
            appendEntriesResponse.Success.ShouldBe(true);
            appendEntriesResponse.Term.ShouldBe(1);
            _log.GetTermAtIndex(1).ShouldBe(1);
        }

        [Fact(DisplayName = "AppendEntries - Follower - 5. If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)")]
        public void FollowerShouldSetCommitIndexIfLeaderCommitGreaterThanCommitIndex()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 1, default(Guid), 0, 0);
            var log = new LogEntry("term 1 commit index 0", typeof(string), 1);
            _log.Apply(log);
            var appendEntriesRpc = new AppendEntriesBuilder()
               .WithEntry(log)
               .WithTerm(1)
               .WithPreviousLogIndex(1)
               .WithPreviousLogTerm(1)
               .WithLeaderCommitIndex(1)
               .Build();
            //assume node has applied log..
            var follower = new Follower(_currentState, _fsm, _log, _random, _node, _settings, _rules);
            var appendEntriesResponse = follower.Handle(appendEntriesRpc);
            follower.CurrentState.CommitIndex.ShouldBe(1);
        }

        [Fact(DisplayName = "AppendEntries - Candidate - 5. If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)")]
        public void CandidateShouldSetCommitIndexIfLeaderCommitGreaterThanCommitIndex()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 0, default(Guid), 0, 0);
            //assume log applied by node?
            var log = new LogEntry("term 1 commit index 0", typeof(string), 1);
            _log.Apply(log);
            var appendEntriesRpc = new AppendEntriesBuilder()
               .WithEntry(log)
               .WithTerm(1)
               .WithPreviousLogIndex(1)
               .WithPreviousLogTerm(1)
               .WithLeaderCommitIndex(1)
               .Build();
            var follower = new Candidate(_currentState, _fsm, _peers, _log, _random, _node, _settings);
            var appendEntriesResponse = follower.Handle(appendEntriesRpc);
            follower.CurrentState.CommitIndex.ShouldBe(1);
        }

        [Fact(DisplayName = "AppendEntries - Leader - 5. If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)")]
        public void LeaderShouldSetCommitIndexIfLeaderCommitGreaterThanCommitIndex()
        {
            _currentState = new CurrentState(Guid.NewGuid(), 0, default(Guid), 0, 0);
            //assume log applied by node?
            var log = new LogEntry("term 1 commit index 0", typeof(string), 1);
            _log.Apply(log);
            var appendEntriesRpc = new AppendEntriesBuilder()
               .WithEntry(log)
               .WithTerm(1)
               .WithPreviousLogIndex(1)
               .WithPreviousLogTerm(1)
               .WithLeaderCommitIndex(1)
               .Build();
            var leader = new Leader(_currentState, _fsm, _peers, _log, _node, _settings);
            var state = leader.Handle(appendEntriesRpc);
            leader.CurrentState.CommitIndex.ShouldBe(1);
        }
    }
}