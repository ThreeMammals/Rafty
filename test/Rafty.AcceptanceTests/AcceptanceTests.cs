using System;
using System.Collections.Generic;
using System.Threading;
using TestStack.BDDfy;
using Xunit;

namespace Rafty.AcceptanceTests
{
    public class AcceptanceTests : IDisposable
    {
        private AcceptanceTestsSteps _s;

        public AcceptanceTests()
        {
            _s = new AcceptanceTestsSteps();
        }

         [Fact]
         public void should_elect_leader()
         {
             var remoteServers = new List<string>
             {
                 "http://localhost:2231",
                 "http://localhost:2232",
                 "http://localhost:2233",
                 "http://localhost:2234",
                 "http://localhost:2235",
             };

             this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                 .When(x => _s.ThenANewLeaderIsElected())
                 .Then(x => _s.ThenTheOtherNodesAreFollowers(4))
                 .BDDfy();
         }


         [Fact]
         public void should_elect_new_leader_if_previous_leader_dies()
         {
             var remoteServers = new List<string>
             {
                 "http://localhost:1331",
                 "http://localhost:1332",
                 "http://localhost:1333",
                 "http://localhost:1334",
                 "http://localhost:1335",
             };

             this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                 .When(x => _s.WhenTheLeaderDies())
                 .Then(x => _s.ThenANewLeaderIsElected())
                 .And(x => _s.ThenTheOtherNodesAreFollowers(3))
                 .BDDfy();
         }

         [Fact]
         public void should_add_new_server_to_cluster_after_leader_is_elected()
         {
              var remoteServers = new List<string>
             {
                 "http://localhost:4231",
                 "http://localhost:4232",
                 "http://localhost:4233",
                 "http://localhost:4234",
                 "http://localhost:4235",
             };

             this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                 .And(x => _s.ThenANewLeaderIsElected())
                 .When(x => _s.WhenIAddANewServer("http://localhost:4236"))
                 .Then(x => _s.ThenThatServerIsReceivingAndSendingMessages("http://localhost:4236"))
                 .BDDfy();
         }

          [Fact]
         public void should_add_new_server_to_cluster()
         {
              var remoteServers = new List<string>
             {
                 "http://localhost:1431",
                 "http://localhost:1432",
                 "http://localhost:1437",
                 "http://localhost:1436",
                 "http://localhost:1435",
             };

             this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                 .When(x => _s.WhenIAddANewServer("http://localhost:1439"))
                 .Then(X => _s.ThenThatServerIsReceivingAndSendingMessages("http://localhost:1439"))
                 .BDDfy();
         }

         [Fact]
        public void after_leader_is_elected_should_persist_command_to_all_servers()
        {
            var remoteServers = new List<string>
            {
                "http://localhost:5231",
                "http://localhost:5232",
                "http://localhost:5233",
                "http://localhost:5234",
                "http://localhost:5235",
            };

            this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                .And(x => _s.ThenANewLeaderIsElected())
                .And(x => _s.ThenTheOtherNodesAreFollowers(4))
                .When(x => _s.AFakeCommandIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(0, 5))
                .BDDfy();
        }

        [Fact]
        public void after_leader_is_elected_should_persist_command_to_all_servers_more_than_once()
        {
            var remoteServers = new List<string>
            {
                "http://localhost:5231",
                "http://localhost:5232",
                "http://localhost:5233",
                "http://localhost:5234",
                "http://localhost:5235",
            };

            this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                .And(x => _s.ThenANewLeaderIsElected())
                .And(x => _s.ThenTheOtherNodesAreFollowers(4))
                .When(x => _s.AFakeCommandIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(0, 5))
                .When(x => _s.AFakeCommandIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(1, 5))
                .When(x => _s.AFakeCommandIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(2, 5))
                .BDDfy();
        }

        [Fact]
        public void after_leader_is_elected_should_persist_different_commands_to_all_servers()
        {
            var remoteServers = new List<string>
            {
                "http://localhost:5231",
                "http://localhost:5232",
                "http://localhost:5233",
                "http://localhost:5234",
                "http://localhost:5235",
            };

            this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                .And(x => _s.ThenANewLeaderIsElected())
                .And(x => _s.ThenTheOtherNodesAreFollowers(4))
                .When(x => _s.AFakeCommandIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(0, 5))
                .When(x => _s.AFakeCommandTwoIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandTwoIsPersistedToAllStateMachines(1, 5))
                .BDDfy();
        }

        [Fact]
        public void follower_should_forward_command_to_leader()
        {
            var remoteServers = new List<string>
            {
                "http://localhost:5231",
                "http://localhost:5232",
                "http://localhost:5233",
                "http://localhost:5234",
                "http://localhost:5235",
            };

            this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                .And(x => _s.ThenANewLeaderIsElected())
                .And(x => _s.ThenTheOtherNodesAreFollowers(4))
                .When(x => _s.ACommandIsSentToAFollower())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(0, 5))
                .BDDfy();
        }

        [Fact]
        public void after_first_leader_dies_and_new_leader_is_elected_should_be_able_to_send_commands_to_all_servers()
        {
            var remoteServers = new List<string>
            {
                "http://localhost:5231",
                "http://localhost:5232",
                "http://localhost:5233",
                "http://localhost:5234",
                "http://localhost:5235",
            };

            this.Given(x => _s.GivenTheFollowingServersAreRunning(remoteServers))
                .And(x => _s.ThenANewLeaderIsElected())
                .And(x => _s.ThenTheOtherNodesAreFollowers(4))
                .When(x => _s.AFakeCommandIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(0, 5))
                .And(x => _s.WhenTheLeaderDies())
                .And(x => _s.ThenANewLeaderIsElected())
                .And(x => _s.ThenTheOtherNodesAreFollowers(3))
                .When(x => _s.AFakeCommandIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(1, 4))
                .When(x => _s.AFakeCommandIsSentToTheLeader())
                .Then(x => _s.ThenTheFakeCommandIsPersistedToAllStateMachines(2, 4))
                .BDDfy();
        }

        public void Dispose()
        {
            _s.Dispose();
        }
    }
}
