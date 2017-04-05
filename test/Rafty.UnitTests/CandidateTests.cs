using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public class CandidateTests
    {
        private Mock<IMessageBus> _messageBus;
        private Server _server;
        private InMemoryServersInCluster _serversInCluster;
        private FakeStateMachine _fakeStateMachine;

        public CandidateTests()
        {
            _messageBus = new Mock<IMessageBus>();
            _serversInCluster = new InMemoryServersInCluster();
        }

        [Fact]
        public void server_should_increment_current_term_on_conversion_to_candidate()
        {
            var remoteServers = new List<ServerInCluster>
            {
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
            };

            this.Given(x => GivenTheFollowingRemoteServers(remoteServers))
                .And(x => GivenANewServer())
                .And(x => TheServerDoesNotReceivesVotesFromAllRemoteServers())
                .When(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => TheServerIsACandidate())
                .And(x => ThenTheCurrentTermIs(1))
                .BDDfy();
        }

        [Fact]
        public void server_should_vote_for_itself_on_conversion_to_candidate()
        {
            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => TheServerVotesForItself())
                .BDDfy();
        }

        [Fact]
        public void server_should_reset_election_timer_on_conversion_to_candidate()
        {
            this.Given(x => GivenANewServer())
                .When(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => ThenTheServerResetsElectionTimer())
                .BDDfy();
        }

        [Fact]
        public void server_should_request_votes_from_all_nodes_conversion_to_candidate()
        {
            var remoteServers = new List<ServerInCluster>
            {
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
            };

            this.Given(x => GivenTheFollowingRemoteServers(remoteServers))
                .And(x => GivenANewServer())
                .And(x => TheServerReceivesVotesFromAllRemoteServers())
                .And(x => x.GivenAllServersAppendEntries())
                .When(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => ThenTheServerRequestsVotesFromAllRemoteServers())
                .BDDfy();
        }

        [Fact]
        public void server_should_become_leader_if_votes_received_from_majority_of_servers()
        {
             var remoteServers = new List<ServerInCluster>
            {
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
            };

            this.Given(x => GivenTheFollowingRemoteServers(remoteServers))
                .And(x => GivenANewServer())
                .And(x => TheServerReceivesVotesFromAllRemoteServers())
                .And(x => x.GivenAllServersAppendEntries())
                .When(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => ThenTheServerIsTheLeader())
                .And(x => ThenTheCurrentTermVotesAre(3))
                .BDDfy();
        }

        private void GivenAllServersAppendEntries()
        {
            var response = _serversInCluster.All.Select(remoteServer => Task.FromResult(new AppendEntriesResponse(_server.CurrentTerm, true, remoteServer.Id, _server.Id))).ToList();

            _messageBus.Setup(x => x.Send(It.IsAny<AppendEntries>())).ReturnsInOrder(response);
        }

        [Fact]
        public void server_should_become_follower_if_receives_append_entries_while_candidate()
        {
            var appendEntries = new AppendEntries(0, Guid.NewGuid(), 0, 0, null, 0, Guid.NewGuid());

            this.Given(x => GivenANewServer())
                .And(x => ServerReceives(new BecomeCandidate(Guid.NewGuid())))
                .When(x => ServerReceives(appendEntries))
                .Then(x => ThenTheServerIsAFollower())
                .And(x => ThenTheCurrentTermVotesAre(0))
                .BDDfy();
        }

        [Fact]
        public void server_should_restart_election_if_election_times_out()
        {
            var remoteServers = new List<ServerInCluster>
            {
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
            };

            this.Given(x => GivenTheFollowingRemoteServers(remoteServers))
                .And(x => GivenANewServer())
                .And(x => TheServerDoesNotReceivesVotesFromAllRemoteServers())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .And(x => TheServerDoesNotReceivesVotesFromAllRemoteServers())
                .When(x => ServerReceives(new BecomeCandidate(Guid.NewGuid())))
                .Then(x => ThenTheServerIsACandidate())
                .And(x => ThenTheCurrentTermVotesAre(1))
                .BDDfy();
        }

        [Fact]
        public void should_not_restart_election_if_received_append_entries_in_current_term()
        {
            this.Given(x => GivenANewServer())
            .And(x => ServerReceives(new BecomeCandidate(Guid.NewGuid())))
            .And(x => ServerReceives(new AppendEntries(1, Guid.NewGuid(), 0, 0, null, 0, Guid.NewGuid())))
            .When(x => ServerReceives(new BecomeCandidate(Guid.NewGuid())))
            .Then(x => ThenTheServerIsAFollower())
            .And(x => ThenTheCurrentTermVotesAre(0))
            .BDDfy();
        }

        private void ThenTheServerIsACandidate()
        {
            _server.State.ShouldBeOfType<Candidate>();
        }

        private void ThenTheCurrentTermVotesAre(int expected)
        {
            _server.CurrentTermVotes.ShouldBe(expected);
        }

        private void ThenTheServerIsAFollower()
        {
            _server.State.ShouldBeOfType<Follower>();
        }

        private void ThenTheServerIsTheLeader()
        {
            _messageBus.Verify(x => x.Publish(It.IsAny<SendToSelf>()));
        }

        private void TheServerReceivesVotesFromAllRemoteServers()
        {
            var response = _serversInCluster.All.Select(remoteServer => Task.FromResult(new RequestVoteResponse(_server.CurrentTerm, true, remoteServer.Id, _server.Id))).ToList();

            _messageBus.Setup(x => x.Send(It.IsAny<RequestVote>())).ReturnsInOrder(response);
        }

         private void TheServerDoesNotReceivesVotesFromAllRemoteServers()
        {
            var response = _serversInCluster.All.Select(remoteServer => Task.FromResult(new RequestVoteResponse(_server.CurrentTerm, false, remoteServer.Id, _server.Id))).ToList();

            _messageBus.Setup(x => x.Send(It.IsAny<RequestVote>())).ReturnsInOrder(response);
        }

        private void GivenTheFollowingRemoteServers(List<ServerInCluster> remoteServers)
        {
            _serversInCluster.Add(remoteServers);
        }

        private void ThenTheServerRequestsVotesFromAllRemoteServers()
        {
            _messageBus.Verify(x => x.Publish(It.IsAny<SendToSelf>()));
        }

        private void ThenTheServerResetsElectionTimer()
        {
            _messageBus.Verify(x => x.Publish(It.IsAny<SendToSelf>()));
        }

        private void TheServerVotesForItself()
        {
            _messageBus.Verify(x => x.Publish(It.IsAny<SendToSelf>()));
        }

        private void ThenTheCurrentTermIs(int expected)
        {
            _server.CurrentTerm.ShouldBe(expected);
        }

        private void ServerReceives(BecomeCandidate becomeCandidate)
        {
            _server.Receive(becomeCandidate);
        }

        private void ServerReceives(AppendEntries appendEntries)
        {
            _server.Receive(appendEntries).Wait();
        }

        private void TheServerIsACandidate()
        {
            _server.State.ShouldBeOfType<Candidate>();
        }

        private void GivenANewServer()
        {
            _fakeStateMachine = new FakeStateMachine();
            _server = new Server(_messageBus.Object, _serversInCluster, _fakeStateMachine, new ConsoleLogger("ConsoleLogger", (x, y) => true, true));
        }
    }
}
