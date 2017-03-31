using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Console;
using Moq;
using Rafty.Commands;
using Rafty.Messages;
using Rafty.Messaging;
using Rafty.Raft;
using Rafty.State;
using Shouldly;
using TestStack.BDDfy;
using Xunit;

namespace Rafty.UnitTests
{
    public class FollowerTests
    {
        private FakeMessageBus _messageBus;
        private readonly Mock<IMessageBus> _messageBusMock;
        private Server _server;
        private List<ServerInCluster> _remoteServers;
        private FakeStateMachine _fakeStateMachine;

        public FollowerTests()
        {
            _messageBusMock = new Mock<IMessageBus>();
        }

        [Fact]
        public void server_should_start_in_follower_state()
        {
            this.Given(x => GivenANewServer())
                .Then(x => TheServerIsAFollower())
                .And(x => ThenTheCurrentTermIs(0))
                .BDDfy();
        }

        [Fact]
        public void server_should_become_candidate_if_election_timer_times_out()
        {
            this.Given(x => GivenANewServer())
                .And(x => ThenTheServerReceivesBecomeCandidate())
                .BDDfy();
        }

        [Fact]
        public void server_should_forward_command_to_leader()
        {
            this.Given(x => GivenANewServer(_messageBusMock))
                .When(x => ServerReceives(new FakeCommand(Guid.NewGuid())))
                .Then(x => ThenTheCommandIsForwardedToTheLeader())
                .BDDfy();
        }

        private void ThenTheCommandIsForwardedToTheLeader()
        {
            _messageBusMock.Verify(x => x.Send(It.IsAny<ICommand>(), It.IsAny<Guid>()), Times.Once);
        }

        private void ServerReceives(FakeCommand fakeCommand)
        {
            _server.Receive(fakeCommand);
        }

        private void ThenTheServerReceivesBecomeCandidate()
        {
            var sendToSelf = (SendToSelf)_messageBus.SendToSelfMessages[0];
            sendToSelf.Message.ShouldBeOfType<BecomeCandidate>();
        }

        private void GivenANewServer()
        {
            _fakeStateMachine = new FakeStateMachine();
            _messageBus = new FakeMessageBus();
            _server = new Server(_messageBus, _remoteServers, _fakeStateMachine, new ConsoleLogger("ConsoleLogger", (x, y) => true, true));
        }

        private void GivenANewServer(Mock<IMessageBus> mock)
        {
            _fakeStateMachine = new FakeStateMachine();
            _server = new Server(mock.Object, _remoteServers, _fakeStateMachine, new ConsoleLogger("ConsoleLogger", (x, y) => true, true));
        }

        private void TheServerIsAFollower()
        {
            _server.State.ShouldBeOfType<Follower>();
        }

        private void ThenTheCurrentTermIs(int expected)
        {
            _server.CurrentTerm.ShouldBe(expected);
        }
    }
}