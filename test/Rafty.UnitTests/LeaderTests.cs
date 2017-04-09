using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Moq;
using Moq.Language.Flow;
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
    public class LeaderTests
    {
        private Mock<IMessageBus> _messageBus;
        private Server _server;
        private InMemoryServersInCluster _serversInCluster;
        private FakeCommand _fakeCommand;
        private FakeStateMachine _fakeStateMachine;


        public LeaderTests()
        {
            _messageBus = new Mock<IMessageBus>();
            _serversInCluster = new InMemoryServersInCluster();
        }

        [Fact]
        public void server_should_send_empty_append_entries_on_election()
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
                .And(x => TheServerReceivesAMajorityOfVotes())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => ThenTheServerReceivesSendHeartbeat())
                .BDDfy();
        }

        [Fact]
        public void server_should_convert_to_follower_if_receives_append_entries_from_another_leader()
        {
            var remoteServers = new List<ServerInCluster>
            {
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
            };

            var appendEntries = new AppendEntries(0, Guid.NewGuid(), 0, 0, null, 0, Guid.NewGuid());

            this.Given(x => GivenTheFollowingRemoteServers(remoteServers))
                .And(x => GivenANewServer())
                .And(x => TheServerReceivesAMajorityOfVotes())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .And(x => ServerReceives(appendEntries))
                .Then(x => ThenTheServerIsAFollower())
                .BDDfy();
        }


        [Fact]
        public void server_should_send_period_heartbeat_if_leader()
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
                .And(x => TheServerReceivesAMajorityOfVotes())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .And(x => TheServerReceivesAppendEntriesResponsesForCommand())
                .When(x => ServerReceives(new SendHeartbeat()))
                .Then(x => ThenTheServerSendsAHeartbeatToAllRemoteServers())
                .And(x => TheServerWillSendAnotherHeartbeatLater())
                .BDDfy();
        }

        [Fact]
        public void server_should_issue_append_entries_to_all_remote_servers_when_it_receives_command_from_client()
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
                .And(x => TheServerReceivesAMajorityOfVotes())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .And(x => TheServerReceivesAppendEntriesResponsesForCommand())
                .When(x => WhenTheServerReceivesACommand(new FakeCommand(Guid.NewGuid())))
                .Then(x => ThenTheServerSendAppendEntriesToEachRemoteServer())
                .And(x => ThenCommandAppendedToLocalLog())
                .BDDfy();
        }

        [Fact]
        public void server_should_commit_to_state_machine_when_command_persisted_to_majority_of_servers()
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
                .And(x => TheServerReceivesAMajorityOfVotes())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .And(x => TheServerReceivesAppendEntriesResponsesForCommand())
                .And(x => WhenTheServerReceivesACommand(new FakeCommand(Guid.NewGuid())))
                .Then(x => TheCommandIsAppliedToTheStateMachine())
                .And(x => ThenTheCurrentTermAppendEntriesResponseIs(0))
                .And(x => ThenTheCommitIndexIs(0))
                .And(x => ThenTheLastAppliedIs(0))
                .BDDfy();
        }


        [Fact]
        public void server_should_be_leader_and_current_term_votes_are_3()
        {
            var remoteServers = new List<ServerInCluster>
            {
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
                new ServerInCluster(Guid.NewGuid()),
            };

            this.Given(x => GivenTheFollowingRemoteServers(remoteServers))
                .And(x => GivenANewServer())
                .And(x => TheServerReceivesAMajorityOfVotes())
                .When(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => ThenTheCurrentTermVotesAre(3))
                .And(x => TheServerIsALeader())
                .BDDfy();
        }

        [Fact]
        public void server_should_initialise_next_index_on_election()
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
                .And(x => TheServerReceivesAMajorityOfVotes())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => ThenTheNextIndexIsInitialisedForEachRemoteServer())
                .BDDfy();
        }

         [Fact]
        public void server_should_initialise_match_index_on_election()
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
                .And(x => TheServerReceivesAMajorityOfVotes())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .Then(x => ThenTheMatchIndexIsInitialisedForEachRemoteServer())
                .BDDfy();
        }

        [Fact]
        public void server_should_update_match_and_next_index_if_append_entries_succesfull()
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
                .When(x => TheServerReceivesAMajorityOfVotes())
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .And(x => TheServerReceivesAppendEntriesResponsesForCommand())
                .And(x => WhenTheServerReceivesACommand(new FakeCommand(Guid.NewGuid())))
                .When(x => WhenTheServerReceivesAMajorityOfResponses())
                .Then(x => ThenTheNextIndexIsUpdated(1))
                .And(x => ThenTheMatchIndexIsUpdated(0))
                .BDDfy();
        }

        [Fact]
        public void server_should_decrement_next_index_and_retry_if_append_entries_fails()
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
                .And(x => TheServerReceivesAMajorityOfVotes())                
                .And(x => ServerReceives(new BecomeCandidate(Guid.Empty)))
                .And(x => TheServerReceivesAppendEntriesResponsesForCommand())
                .And(x => WhenTheServerReceivesACommand(new FakeCommand(Guid.NewGuid())))
                .When(x => WhenTheServerReceivesFailThenASuccessFromARemoteServer(remoteServers[2]))
                .Then(x => ThenTheAppendEntriesIsRetried())
                .BDDfy();
        }

        private void ServerReceives(BecomeCandidate becomeCandidate)
        {
            _server.Receive(becomeCandidate);
        }

        private void ThenTheAppendEntriesIsRetried()
        {
            _messageBus.Verify(x => x.Send(It.IsAny<AppendEntries>()), Times.Exactly(10));
        }

        private void WhenTheServerReceivesFailThenASuccessFromARemoteServer(ServerInCluster serverInCluster)
        {
            var fail = new AppendEntriesResponse(_server.CurrentTerm, false, serverInCluster.Id, _server.Id);

            var success = new AppendEntriesResponse(_server.CurrentTerm, true, serverInCluster.Id, _server.Id);

            _messageBus.Setup(x => x.Send(It.IsAny<AppendEntries>())).ReturnsInOrder(Task.FromResult(fail), Task.FromResult(success));
        }

        private void ThenTheCommitIndexIs(int expected)
        {
            _server.CommitIndex.ShouldBe(expected);
        }

        private void ThenTheLastAppliedIs(int expected)
        {
            _server.LastApplied.ShouldBe(expected);
        }

        private void ThenTheNextIndexIsUpdated(int expected)
        {
            foreach(var remoteSever in _serversInCluster.All.Where(x => x.Id != _server.Id))
            {
                var next = _server.NextIndex.First(x => x.Id == remoteSever.Id);
                next.NextIndex.ShouldBe(expected);
            }
        }

        private void ThenTheMatchIndexIsUpdated(int expected)
        {
            foreach(var remoteSever in _serversInCluster.All.Where(x => x.Id != _server.Id))
            {
                var match = _server.MatchIndex.First(x => x.Id == remoteSever.Id);
                match.MatchIndex.ShouldBe(expected);
            }
        }

        private void ThenTheMatchIndexIsInitialisedForEachRemoteServer()
        {
            foreach(var remoteServer in _serversInCluster.All.Where(x => x.Id != _server.Id))
            {
                var match = _server.MatchIndex.First(x => x.Id == remoteServer.Id);
                match.MatchIndex.ShouldBe(0);
            }
        }

        private void ThenTheNextIndexIsInitialisedForEachRemoteServer()
        {
            foreach(var remoteServer in _serversInCluster.All.Where(x => x.Id != _server.Id))
            {
                var next = _server.NextIndex.First(x => x.Id == remoteServer.Id);
                next.NextIndex.ShouldBe(0);
            }
        }

        private void TheCommandIsAppliedToTheStateMachine()
        {
            _fakeStateMachine.Commands[0].ShouldBe(_fakeCommand);
        }

        private void ThenTheCurrentTermAppendEntriesResponseIs(int expected)
        {
            _server.CurrentTermAppendEntriesResponse.ShouldBe(expected);
        }

        private void WhenTheServerReceivesAMajorityOfResponses()
        {
            var response = _serversInCluster.All.Select(remoteServer => Task.FromResult(new AppendEntriesResponse(_server.CurrentTerm, true, remoteServer.Id, _server.Id))).ToList();

            _messageBus.Setup(x => x.Send(It.IsAny<AppendEntries>())).ReturnsInOrder(response);
        }

        private void ThenCommandAppendedToLocalLog()
        {
            _server.Log[0].Command.ShouldBe(_fakeCommand);
        }

        private void ThenTheServerIsAFollower()
        {
            _server.State.ShouldBeOfType<Follower>();
        }

        private void ServerReceives(AppendEntries appendEntries)
        {
            _server.Receive(appendEntries).Wait();
        }

        private void ThenTheServerSendAppendEntriesToEachRemoteServer()
        {
            _messageBus.Verify(x => x.Send
            (It.IsAny<AppendEntries>()), Times.Exactly(10));
        }

        private void WhenTheServerReceivesACommand(FakeCommand fakeCommand)
        {
            _fakeCommand = fakeCommand;
            _server.Receive(fakeCommand).Wait();
        }

        private void TheServerWillSendAnotherHeartbeatLater()
        {
            _messageBus.Verify(x => x.Publish(It.IsAny<SendToSelf>()));
        }

        private void ThenTheServerSendsAHeartbeatToAllRemoteServers()
        {
            _messageBus.Verify(x => x.Publish(It.IsAny<SendToSelf>()));
        }

        private void ThenTheServerReceivesSendHeartbeat()
        {
            _messageBus.Verify(x => x.Publish(It.IsAny<SendToSelf>()));
        }

        private void TheServerReceivesAppendEntriesResponsesForCommand()
        {
            var response = _serversInCluster.All.Select(remoteServer => Task.FromResult(new AppendEntriesResponse(_server.CurrentTerm, true, remoteServer.Id, _server.Id))).ToList();
            
            _messageBus.Setup(x => x.Send(It.IsAny<AppendEntries>())).ReturnsInOrder(response);
        }

        private void TheServerReceivesAMajorityOfVotes()
        {
            var requestVoteResponses = _serversInCluster.All.Select(x => Task.FromResult(new RequestVoteResponse(0, true, x.Id, Guid.NewGuid()))).ToList();

            _messageBus.Setup(x => x.Send(It.IsAny<RequestVote>())).ReturnsInOrder(requestVoteResponses);
            
            var response = _serversInCluster.All.Select(remoteServer => Task.FromResult(new AppendEntriesResponse(_server.CurrentTerm, true, remoteServer.Id, _server.Id))).ToList();

            _messageBus.Setup(x => x.Send(It.IsAny<AppendEntries>())).ReturnsInOrder(response);
        }

        private void ServerReceives(SendHeartbeat heartbeat)
        {
            _server.Receive(heartbeat);
        }

        private void GivenTheFollowingRemoteServers(List<ServerInCluster> remoteServers)
        {
            _serversInCluster.Add(remoteServers);
        }

        private void ThenTheCurrentTermVotesAre(int expected)
        {
            _server.CurrentTermVotes.ShouldBe(expected);
        }

        private void TheServerIsALeader()
        {
            _server.State.ShouldBeOfType<Leader>();
        }

        private void GivenANewServer()
        {
            _fakeStateMachine = new FakeStateMachine();
            _server = new Server(_messageBus.Object, _serversInCluster, _fakeStateMachine, new LoggerFactory());
            _serversInCluster.Add(new ServerInCluster(_server.Id));
        }
    }

    public static class MoqExtensions
    {
        public static void ReturnsInOrder<T, TResult>(this ISetup<T, TResult> setup,
          params TResult[] results) where T : class
        {
            setup.Returns(new Queue<TResult>(results).Dequeue);
        }

        public static void ReturnsInOrder<T, TResult>(this ISetup<T, TResult> setup,
           List<TResult> results) where T : class
        {
            setup.Returns(new Queue<TResult>(results).Dequeue);
        }
    }
}