using Xunit;
using TestStack.BDDfy;
using Shouldly;
using Rafty.Concensus;
using System;
using System.Collections.Generic;
using Rafty.Log;

namespace Rafty.UnitTests
{
    public class AllServersApplyToStateMachineTests
    {
/*
• If commitIndex > lastApplied: increment lastApplied, apply
log[lastApplied] to state machine (§5.3)\
*/

        // [Fact]
        // public void Test()
        // {
        //     var currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid), TimeSpan.FromSeconds(0), new InMemoryLog(), -1, -1);
        //     var sendToSelf = new TestingSendToSelf();
        //     var follower = new Follower(currentState, sendToSelf);
        //     var state = follower.Handle(new AppendEntriesBuilder()
        //         .WithTerm(1)
        //         .WithEntry(new LogEntry("test", typeof(string), 1, 0))
        //         .Build());
        //     state.ShouldBeOfType<Follower>();
        //     state.CurrentState.CurrentTerm.ShouldBe(1);
        //     state.CurrentState.LastApplied.ShouldBe(0);
        // }
    }
}