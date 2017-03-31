using System;
using System.Collections.Generic;
using Rafty.AcceptanceTests;

namespace Rafty.ManualTests
{
    class Program
    {

        static int Main(string[] args)
        {
            Console.WriteLine("Starting Rafty");

            var steps = new AcceptanceTestsSteps();

            var remoteServers = new List<string>
            {
                "http://localhost:1231",
                "http://localhost:1232",
                "http://localhost:1233",
                "http://localhost:1234",
                "http://localhost:1235",
            };

            steps.GivenTheFollowingServersAreRunning(remoteServers);
            Console.WriteLine("GivenTheFollowingServersAreRunning finished");

            //var timer = steps.GivenIHaveStartedMonitoring();

            steps.ThenANewLeaderIsElected();
            Console.WriteLine("ThenANewLeaderIsElected finished");

            steps.ThenTheOtherNodesAreFollowers(4);
            Console.WriteLine("ThenTheOtherNodesAreFollowers finished");

            steps.ACommandIsSentToAFollower();
            Console.WriteLine("ACommandIsSentToTheLeader finished");

            steps.TheCommandIsPersistedToAllStateMachines(0, 5);
            Console.WriteLine("TheCommandIsPersistedToAllStateMachines finished");

            //timer.Dispose();
            steps.Dispose();
            return 1;
        }
    }
}