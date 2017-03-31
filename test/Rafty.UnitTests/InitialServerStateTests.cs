using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Console;
using Rafty.Messaging;
using Rafty.Raft;
using Rafty.ServiceDiscovery;
using Rafty.State;
using Shouldly;
using TestStack.BDDfy;
using Xunit;

namespace Rafty.UnitTests
{
    public class InitialServerStateTests
    {
        private Server _server;
        private FakeMessageBus _messageBus;
        private List<ServerInCluster> _remoteServers;
        private FakeStateMachine _fakeStateMachine;

        [Fact]
        public void server_should_have_current_term_of_zero_on_init()
        {
            this.Given(x => GivenANewServer())
                .Then(x => ThenTheCurrentTermIs(0))
                .BDDfy();
        }

        [Fact]
        public void server_should_have_an_id_on_init()
        {
            this.Given(x => GivenANewServer())
                .Then(x => ThenTheServerHasAnId())
                .BDDfy();
        }

        [Fact]
        public void server_should_have_voted_for_of_default_on_init()
        {
            this.Given(x => GivenANewServer())
                .Then(x => ThenTheVotedForIs(default(Guid)))
                .BDDfy();
        }

        [Fact]
        public void server_should_have_commit_log_on_init()
        {
            this.Given(x => GivenANewServer())
                 .Then(x => ThenTheCommitLogContainsCommandsCount(0))
                 .BDDfy();
        }

        [Fact]
        public void server_should_have_commit_index_of_zero_on_init()
        {
            this.Given(x => GivenANewServer())
                 .Then(x => ThenTheCommitIndexIs(0))
                 .BDDfy();
        }

        [Fact]
        public void server_should_have_last_applied_of_zero_on_init()
        {
            this.Given(x => GivenANewServer())
                 .Then(x => ThenTheLastAppliedIs(0))
                 .BDDfy();
        }

        [Fact]
        public void server_should_have_empty_next_index_on_init()
        {
            this.Given(x => GivenANewServer())
                 .Then(x => ThenTheNextIndexIs(new List<int>()))
                 .BDDfy();
        }

        [Fact]
        public void server_should_have_empty_match_index_on_init()
        {
            this.Given(x => GivenANewServer())
                 .Then(x => ThenTheMatchIndexIs(new List<int>()))
                 .BDDfy();
        }

        private void ThenTheMatchIndexIs(List<int> expected)
        {
            _server.MatchIndex.Select(x => x.MatchIndex).ShouldBe(expected);
        }

        private void ThenTheNextIndexIs(List<int> expected)
        {
            _server.NextIndex.Select(x => x.NextIndex).ToList().ShouldBe(expected);
        }

        private void ThenTheLastAppliedIs(int expected)
        {
            _server.LastApplied.ShouldBe(expected);
        }

        private void ThenTheCommitIndexIs(int expected)
        {
            _server.CommitIndex.ShouldBe(expected);
        }

        private void ThenTheCommitLogContainsCommandsCount(int expected)
        {
            _server.Log.Count.ShouldBe(expected);
        }

        private void ThenTheVotedForIs(Guid guid)
        {
            _server.VotedFor.ShouldBe(guid);
        }

        private void ThenTheCurrentTermIs(int expected)
        {
            _server.CurrentTerm.ShouldBe(expected);
        }

        private void GivenANewServer()
        {
            _fakeStateMachine = new FakeStateMachine();
            _messageBus = new FakeMessageBus();
            _server = new Server(_messageBus, _remoteServers, _fakeStateMachine, new ConsoleLogger("ConsoleLogger", (x, y) => true, true));
        }

        private void ThenTheServerHasAnId()
        {
            _server.Id.ShouldNotBe(default(Guid));
        }
    }
}
