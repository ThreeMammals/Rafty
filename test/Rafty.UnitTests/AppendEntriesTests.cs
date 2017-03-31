using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Console;
using Rafty.Commands;
using Rafty.Messages;
using Rafty.Messaging;
using Rafty.Raft;
using Rafty.Responses;
using Rafty.State;
using Shouldly;
using TestStack.BDDfy;
using Xunit;

namespace Rafty.UnitTests
{
    public class AppendEntriesTests
    {
        private Server _server;
        private FakeMessageBus _messageBus;
        private List<ServerInCluster> _remoteServers;
        private FakeStateMachine _fakeStateMachine;
        private AppendEntriesResponse _result;

        [Fact]
        public void server_should_reply_true_if_entries_is_empty()
        {
            var id = Guid.NewGuid();
            var appendEntries = new AppendEntries(0, id, 0, 0, null, 0, Guid.NewGuid());
            var expected = new AppendEntriesResponse(0, true, id, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(appendEntries))
                .Then(x => ThenTheReplyIs(expected))
                .And(x => ThenTheCurrentTermIs(0))
                .BDDfy();
        }

        [Fact]
        public void server_should_set_current_term_as_message_term_if_greater_than_current_term()
        {
            var id = Guid.NewGuid();
            var appendEntries = new AppendEntries(1, id, 0, 0, null,0, Guid.NewGuid());
            var expected = new AppendEntriesResponse(1, true, id, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(appendEntries))
                .Then(x => ThenTheReplyIs(expected))
                .And(x => ThenTheCurrentTermIs(1))
                .BDDfy();
        }

        [Fact]
        public void server_should_reply_false_if_term_is_less_than_current_term()
        {
            var entries = new Log(1, new FakeCommand(Guid.NewGuid()));
            var id = Guid.NewGuid();
            var appendEntries = new AppendEntries(0, id, 0, 0, entries, 0, Guid.NewGuid());
            var expected = new AppendEntriesResponse(1, false, id, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .And(x => GivenTheCurrentTermIs(1))
                .When(x => ServerReceives(appendEntries))
                .Then(x => ThenTheReplyIs(expected))
                .BDDfy();
        }

        [Fact]
        public void server_should_append_new_entries_not_in_log_and_reply_true()
        {
            var entries = new Log(0, new FakeCommand(Guid.NewGuid()));
            var id = Guid.NewGuid();
            var appendEntries = new AppendEntries(0, id, 1, 0, entries, 0, Guid.NewGuid());
            var expected = new AppendEntriesResponse(0, true, id, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(appendEntries))
                .Then(x => ThenTheReplyIs(expected))
                .And(x => ThenTheLogContainsEntriesCount(1))
                .BDDfy();
        }


        [Fact]
        public void
            server_should_reply_false_if_log_doesnt_contain_an_entry_at_previous_log_index_matching_previous_log_term()
        {
            var entries = new Log(0, new FakeCommand(Guid.NewGuid()));
            
            var id = Guid.NewGuid();

            var appendEntries = new AppendEntries(0, id, 0, 0, entries, 0, Guid.NewGuid());

            var oldAppendEntries = new AppendEntries(0, id, 0, 1, entries, 0, Guid.NewGuid());

            var expected = new AppendEntriesResponse(0, false, id, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .And(x => GivenTheServerRecieves(appendEntries))
                .When(x => ServerReceives(oldAppendEntries))
                .Then(x => ThenTheReplyIs(expected))
                .BDDfy();
        }

        [Fact]
        public void server_should_delete_existing_entry_and_all_that_follow_if_existing_entry_conflicts_with_a_new_one()
        {
            var initialEntries = new Log(0, new FakeCommand(Guid.NewGuid()));

            var initialAppendEntries = new AppendEntries(0, Guid.NewGuid(), 0, 0, initialEntries, 0, Guid.NewGuid());

            var newEntries = new Log(1, new FakeCommand(Guid.NewGuid()));

            var id = Guid.NewGuid();

            var newAppendEntries = new AppendEntries(0, id, 0, 0, newEntries, 0, Guid.NewGuid());

            var expected = new AppendEntriesResponse(0, true, id, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .And(x => GivenTheServerRecieves(initialAppendEntries))
                .When(x => ServerReceives(newAppendEntries))
                .Then(x => ThenTheReplyIs(expected))
                .And(x => ThenTheLogCountIs(1))
                .BDDfy();
        }


        [Fact]
        public void server_should_receive_multiple_append_entries()
        {
            var entry = new Log(0, new FakeCommand(Guid.NewGuid()));

            var id = Guid.NewGuid();

            var first = new AppendEntries(0, id, 0, 0, entry, 0, Guid.NewGuid());

            var second = new AppendEntries(0, id, 0, 0, entry, 0, Guid.NewGuid());

            var third = new AppendEntries(0, id, 0, 0, entry, 0, Guid.NewGuid());

            var expected = new AppendEntriesResponse(0, true, id, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .And(x => GivenTheServerRecieves(first))
                .When(x => ServerReceives(second))
                .And(x => ServerReceives(second))
                .Then(x => ThenTheReplyIs(expected))
                .And(x => ThenTheLogCountIs(3))
                .BDDfy();
        }

        [Fact]
        public void should_set_commit_index_if_leader_commit_greater_than_commit_index()
        {
            var entries = new Log(0, new FakeCommand(Guid.NewGuid()));
            var id = Guid.NewGuid();
            var appendEntries = new AppendEntries(0, id, 1, 0, entries, 1, Guid.NewGuid());
            var expected = new AppendEntriesResponse(0, true, id, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(appendEntries))
                .Then(x => ThenTheReplyIs(expected))
                .And(x => ThenTheCommitIndexIs(1))
                .BDDfy();
        }

        private void ThenTheCommitIndexIs(int expected)
        {
            _server.CommitIndex.ShouldBe(expected);
        }

        private void ThenTheLogCountIs(int expected)
        {
            _server.Log.Count.ShouldBe(expected);
        }

        private void GivenTheServerRecieves(AppendEntries appendEntries)
        {
            _server.Receive(appendEntries);
        }

        private void GivenTheCurrentTermIs(int term)
        {
            _server.Receive(new AppendEntries(term, Guid.NewGuid(), 0, 0, null, 0, Guid.NewGuid()));
        }

        private void ThenTheCurrentTermIs(int expected)
        {
            _server.CurrentTerm.ShouldBe(expected);
        }

        private void ThenTheReplyIs(AppendEntriesResponse expected)
        {
            _result.Success.ShouldBe(expected.Success);
            _result.Term.ShouldBe(expected.Term);
        }

        private void ServerReceives(AppendEntries appendEntries)
        {
            _result = _server.Receive(appendEntries);
        }

        private void GivenANewServer()
        {
            _fakeStateMachine = new FakeStateMachine();
            _messageBus = new FakeMessageBus();
            _server = new Server(_messageBus, _remoteServers, _fakeStateMachine, new ConsoleLogger("ConsoleLogger", (x, y) => true, true));
        }

        private void ThenTheLogContainsEntriesCount(int expectedCount)
        {
            _server.Log.Count.ShouldBe(expectedCount);
        }
    }
}
