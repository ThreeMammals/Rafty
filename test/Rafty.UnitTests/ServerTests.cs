using System;
using Moq;
using Shouldly;
using Xunit;

namespace Rafty.UnitTests
{
    public class ServerTests
    {
        private Server _server;

        public ServerTests()
        {
            _server = new Server(Guid.NewGuid());
        }
        
        [Fact]
        public void ShouldStartAsFollower()
        {            
            _server.State.ShouldBe(State.Follower);
        }

        [Fact]
        public void CurrentTermShouldBeInitialisedToZero()
        {
            _server.CurrentTerm.ShouldBe(0);
        }

        [Fact]
        public void VotedForShouldBeInitialisedToNone()
        {
            _server.VotedFor.ShouldBe(default(Guid));
        }

        [Fact]
        public void CommitIndexShouldBeInitialisedToZero()
        {
            _server.CommitIndex.ShouldBe(0);
        }

        [Fact]
        public void LastAppliedShouldBeInitialisedToZero()
        {
            _server.LastApplied.ShouldBe(0);
        }

        [Fact]
        public void ShouldBecomeCandidateAfterTimeOut()
        {
            _server.TimeOut();
            _server.State.ShouldBe(State.Candidate);
        }

        [Fact]
        public void ShouldBecomeLeader()
        {
            _server.BecomeLeader();
            _server.State.ShouldBe(State.Leader);
        }
    }
}