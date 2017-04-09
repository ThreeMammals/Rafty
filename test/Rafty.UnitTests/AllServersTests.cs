using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Moq;
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
    public class AllServersTests
    {
        private Mock<IMessageBus>_messageBus;
        private Server _server;
        private IServersInCluster _serversInCluster;
        private FakeStateMachine _fakeStateMachine;

        public AllServersTests()
        {
            _messageBus = new Mock<IMessageBus>();
            _serversInCluster = new InMemoryServersInCluster();
        }

        [Fact]
        public void server_should_become_follower_if_receives_greater_term_in_append_entries_message()
        {
            var appendEntries = new AppendEntries(20, Guid.NewGuid(), 0,0, null, 0, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(appendEntries))
                .Then(x => ThenTheCurrentTermIs(20))
                .And(x => TheServerIsAFollower())
                .BDDfy();
        }

        [Fact]
        public void server_should_become_follower_if_receives_greater_term_in_append_entries_response_message()
        {
            var appendEntriesResponse = new AppendEntriesResponse(20, false, Guid.NewGuid(), Guid.NewGuid());
            var sendHeartbeat = new SendHeartbeat();
            var remoteServers = new List<ServerInCluster>
            {
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
            };

            var requestVoteResponses = remoteServers.Select(x => Task.FromResult(new RequestVoteResponse(0, true, x.Id, remoteServers[0].Id))).ToList();

            this.Given(x => GivenTheFollowingRemoteServers(remoteServers))
                .And(x => GivenANewServer())
                .And(x => x.TheResponseIs(appendEntriesResponse))
                .And(x => x.TheResponseIs(requestVoteResponses))
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .When(x => ServerReceives(sendHeartbeat))
                .Then(x => ThenTheCurrentTermIs(20))
                .And(x => TheServerIsAFollower())
                .BDDfy();
        }

        private void GivenTheFollowingRemoteServers(List<ServerInCluster> remoteServers)
        {
            _serversInCluster.Add(remoteServers);
        }

        private void ServerReceives(BecomeCandidate becomeCandidate)
        {
            _server.Receive(becomeCandidate);
        }

        [Fact]
        public void server_should_become_follower_if_receives_greater_term_in_request_vote_message()
        {
            var requestVote = new RequestVote(20, Guid.NewGuid(), 0, 0, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(requestVote))
                .Then(x => ThenTheCurrentTermIs(20))
                .And(x => TheServerIsAFollower())
                .BDDfy();
        }

         [Fact]
        public void server_should_become_follower_if_receives_greater_term_in_request_vote_response_message()
        {
            var appendEntriesResponse = new AppendEntriesResponse(20, false, Guid.NewGuid(), Guid.NewGuid());

            var remoteServers = new List<ServerInCluster>
            {
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
            };

            var requestVoteResponses = remoteServers.Select(x => Task.FromResult(new RequestVoteResponse(0, true, x.Id, remoteServers[0].Id))).ToList();

            this.Given(x => GivenTheFollowingRemoteServers(remoteServers))
                .And(x => GivenANewServer())
                .And(x => x.TheResponseIs(appendEntriesResponse))
                .And(x => x.TheResponseIs(requestVoteResponses))
                .When(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => ThenTheCurrentTermIs(20))
                .And(x => TheServerIsAFollower())
                .BDDfy();
        }

        [Fact]
        public void should_add_remote_server_if_receive_append_entries_from_unknown_remote_server()
        {
            var appendEntries = new AppendEntries(20, Guid.NewGuid(), 0,0, null, 0, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(appendEntries))
                .Then(x => TheRemoteServerCountIs(1))
                .BDDfy();
        }

        [Fact]
        public void should_add_remote_server_if_receive_request_votes_from_unknown_remote_server()
        {
            var requestVote = new RequestVote(0, Guid.NewGuid(), 0, 0, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(requestVote))
                .Then(x => TheRemoteServerCountIs(1))
                .BDDfy();
        }

        private void TheResponseIs(RequestVoteResponse requestVoteResponse)
        {
            _messageBus.Setup(x => x.Send(It.IsAny<RequestVote>())).ReturnsAsync(requestVoteResponse);
        }

        private void TheResponseIs(List<Task<RequestVoteResponse>> requestVoteResponse)
        {
            _messageBus.Setup(x => x.Send(It.IsAny<RequestVote>())).ReturnsInOrder(requestVoteResponse);
        }

        private void TheResponseIs(AppendEntriesResponse appendEntriesResponse)
        {
            _messageBus.Setup(x => x.Send(It.IsAny<AppendEntries>())).ReturnsAsync(appendEntriesResponse);
        }

        private void TheRemoteServerCountIs(int expected)
        {
            _serversInCluster.Count.ShouldBe(expected);
        }

        private void TheServerIsAFollower()
        {
            _server.State.ShouldBeOfType<Follower>();
        }

        private void ThenTheCurrentTermIs(int expected)
        {
            _server.CurrentTerm.ShouldBe(expected);
        }

        private void ServerReceives(AppendEntries appendEntries)
        {
            _server.Receive(appendEntries).Wait();
        }

        private void ServerReceives(SendHeartbeat sendHeartbeat)
        {
            _server.Receive(sendHeartbeat);
        }

        private void ServerReceives(RequestVote requestVote)
        {
            _server.Receive(requestVote);
        }

         private void GivenANewServer()
        {
            _fakeStateMachine = new FakeStateMachine();
            _server = new Server(_messageBus.Object, _serversInCluster, _fakeStateMachine, new LoggerFactory());
        }
    }
}