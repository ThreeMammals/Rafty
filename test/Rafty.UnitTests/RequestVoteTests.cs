using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Rafty.AcceptanceTests;
using Rafty.Messages;
using Rafty.Messaging;
using Rafty.Raft;
using Rafty.Responses;
using Rafty.ServiceDiscovery;
using Rafty.State;
using Shouldly;
using TestStack.BDDfy;
using Xunit;

namespace Rafty.UnitTests
{
    public class RequestVoteTests
    {
        private Server _server;
        private FakeMessageBus _messageBus;
        private IServersInCluster _serversInCluster;
        private FakeStateMachine _fakeStateMachine;
        private RequestVoteResponse _result;

        public RequestVoteTests()
        {
            _serversInCluster = new InMemoryServersInCluster();
        }

        [Fact]
        public void server_should_reply_false_if_term_is_less_than_current_term()
        {
            var requestVote = new RequestVote(1, Guid.NewGuid(), 0, 0, Guid.NewGuid());
            var expected = new RequestVoteResponse(2, false, Guid.NewGuid(), Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .And(x => GivenTheCurrentTermIs(2))
                .When(x => WhenServerReceives(requestVote))
                .Then(x => x.ThenTheReplyIs(expected))
                .BDDfy();
        }

        [Fact]
        public void server_should_reply_true_if_voted_for_is_null()
        {
            var requestVote = new RequestVote(1, Guid.NewGuid(), 0, 0, Guid.NewGuid());
            var expected = new RequestVoteResponse(1, true, Guid.NewGuid(), Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => WhenServerReceives(requestVote))
                .Then(x => x.ThenTheReplyIs(expected))
                .BDDfy();
        }

        [Fact]
        public void server_should_reply_false_if_voted_for_is_another_candidate()
        {
            var firstRequestVote = new RequestVote(1, Guid.NewGuid(), 0, 0, Guid.NewGuid());
            var secondRequestVote = new RequestVote(1, Guid.NewGuid(), 0, 0, Guid.NewGuid());

            var expected = new RequestVoteResponse(1, false, Guid.NewGuid(), Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .And(x => GivenTheServerReceives(firstRequestVote))
                .When(x => WhenServerReceives(secondRequestVote))
                .Then(x => x.ThenTheReplyIs(expected))
                .BDDfy();
        }

        [Fact]
        public void server_should_reply_true_if_candidate_id_and_log_is_up_to_date_with_server()
        {
            var firstRequestVote = new RequestVote(1, Guid.NewGuid(), 0, 0, Guid.NewGuid());

            var expected = new RequestVoteResponse(1, true, Guid.NewGuid(), Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => WhenServerReceives(firstRequestVote))
                .Then(x => x.ThenTheReplyIs(expected))
                .BDDfy();
        }

        [Fact]
        public void server_should_reply_false_if_candidate_id_and_log_is_not_up_to_date_with_server()
        {
            var firstRequestVote = new RequestVote(0, Guid.NewGuid(), 0, 0, Guid.NewGuid());

            var expected = new RequestVoteResponse(1, false, Guid.NewGuid(), Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .And(x => GivenTheCandidatesLogIsAtIndex(1))
                .When(x => WhenServerReceives(firstRequestVote))
                .Then(x => x.ThenTheReplyIs(expected))
                .BDDfy();
        }

        private void GivenTheCandidatesLogIsAtIndex(int index)
        {
            var entries = new Log(1, new FakeCommand(Guid.NewGuid()));

            var appendEntries = new AppendEntries(1, Guid.NewGuid(), index, 0, entries, 0, Guid.NewGuid());

            _server.Receive(appendEntries).Wait();
        }

        private void ThenTheReplyIs(RequestVoteResponse expected)
        {
            _result.Term.ShouldBe(expected.Term);
            _result.VoteGranted.ShouldBe(expected.VoteGranted);
        }

        private void GivenTheServerReceives(RequestVote requestVote)
        {
            WhenServerReceives(requestVote);
        }

        private void WhenServerReceives(RequestVote requestVote)
        {
            _result = _server.Receive(requestVote);
        }

        private void GivenANewServer()
        {
            _fakeStateMachine = new FakeStateMachine();
            _messageBus = new FakeMessageBus();
            _server = new Server(_messageBus, _serversInCluster, _fakeStateMachine, new LoggerFactory());
        }

        private void GivenTheCurrentTermIs(int term)
        {
            _server.Receive(new AppendEntries(term, Guid.NewGuid(), 0, 0, null, 0, Guid.NewGuid())).Wait();
        }
    }
}
