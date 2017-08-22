using System;
using System.Collections.Generic;
using Rafty.FiniteStateMachine;
using Rafty.Log;

namespace Rafty.Concensus
{
    public class Node : INode
    {
        private readonly IFiniteStateMachine _fsm;
        private readonly ILog _log;
        private readonly List<IPeer> _peers;
        private readonly IRandomDelay _random;
        private readonly Settings _settings;

        public Node(IFiniteStateMachine fsm, ILog log, List<IPeer> peers, IRandomDelay random, Settings settings)
        {
            _fsm = fsm;
            _log = log;
            _peers = peers;
            _random = random;
            _settings = settings;
            BecomeFollower(new CurrentState(Guid.NewGuid(), 0, default(Guid), 0, 0));
        }

        public IState State { get; private set; }

        public void BecomeCandidate(CurrentState state)
        {
            State.Stop();
            var candidate = new Candidate(state, _fsm, _peers, _log, _random, this, _settings);
            State = candidate;
            candidate.BeginElection();
        }

        public void BecomeLeader(CurrentState state)
        {
            State.Stop();
            State = new Leader(state, _fsm, _peers, _log, this, _settings);
        }

        public void BecomeFollower(CurrentState state)
        {
            State?.Stop();
            State = new Follower(state, _fsm, _log, _random, this, _settings);
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            return State.Handle(appendEntries);
        }

        public RequestVoteResponse Handle(RequestVote requestVote)
        {
            return State.Handle(requestVote);
        }
    }
}