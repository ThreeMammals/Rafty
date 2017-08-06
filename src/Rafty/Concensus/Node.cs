/*namespace Rafty.Concensus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Rafty.FiniteStateMachine;
    using Rafty.Log;

    public class Node : IDisposable, INode 
    {
        private readonly List<Guid> _appendEntriesIdsReceived;
        private readonly ISendToSelf _sendToSelf;
        private Guid _appendEntriesAtPreviousHeartbeat;
        private IFiniteStateMachine _fsm;
        private ILog _log;

        private IRandomDelay _random;

        public Node(ISendToSelf sendToSelf, IFiniteStateMachine fsm, ILog log, IRandomDelay random)
        {
            _random = random;
            _appendEntriesIdsReceived = new List<Guid>();
            _sendToSelf = sendToSelf;
            _fsm = fsm;
            _log = log;
            Id = Guid.NewGuid();
        }

        public void Start(List<IPeer> peers, TimeSpan timeout)
        {
            var initialState = new CurrentState(Id, 0, default(Guid), timeout, -1, -1);
            State = new Follower(initialState, _sendToSelf, _fsm, peers, _log, _random);
            //this should be a random timeout which will help get the elections going at different times..
            var delay = _random.Get(100, Convert.ToInt32(initialState.Timeout.TotalMilliseconds));
            _sendToSelf.Publish(new Timeout(delay));
        }

        public void Dispose()
        {
            _sendToSelf.Dispose();
        }
        
        public Node(IState state)
        {
            this.State = state;

        }
/*        public IState State { get; private set; }

        public Guid Id {get;private set;}

        public void Handle(Message message)
        {
            //todo - could run middleware type functions here?
            //todo - these handlers should be in a dictionary
            if (message.GetType() == typeof(BeginElection))
            {
                Handle((BeginElection)message);
            }

            if (message.GetType() == typeof(Timeout))
            {
                Handle((Timeout)message);
            }
        }

        //todo - needs test coverage
        public Response<T> Accept<T>(T command)
        {
            return State.Accept(command);
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            //Reply false if term < currentTerm (§5.1)
            if (appendEntries.Term < State.CurrentState.CurrentTerm)
            {
                return new AppendEntriesResponse(State.CurrentState.CurrentTerm, false);
            }

            // Reply false if log doesn’t contain an entry at prevLogIndex whose term matches prevLogTerm (§5.3)
            var termAtPreviousLogIndex = _log.GetTermAtIndex(appendEntries.PreviousLogIndex);
            if (termAtPreviousLogIndex != appendEntries.PreviousLogTerm)
            {
                return new AppendEntriesResponse(State.CurrentState.CurrentTerm, false);
            }

            //If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it(§5.3)
            foreach (var log in appendEntries.Entries)
            {
                _log.DeleteConflictsFromThisLog(log);
            }

            //Append any new entries not already in the log
            foreach (var log in appendEntries.Entries)
            {
                _log.Apply(log);
            }

            //todo - not sure if this should be first thing we do or its in correct place now.
            State = State.Handle(appendEntries);

            _appendEntriesIdsReceived.Add(appendEntries.MessageId);

            return new AppendEntriesResponse(State.CurrentState.CurrentTerm, true);
        }

        public RequestVoteResponse Handle(RequestVote requestVoteRpc)
        {
            //Reply false if term<currentTerm
            if (requestVoteRpc.Term < State.CurrentState.CurrentTerm)
            {
                return new RequestVoteResponse(false, State.CurrentState.CurrentTerm);
            }

            //Reply false if voted for is not candidateId
            //Reply false if voted for is not default
            if (State.CurrentState.VotedFor == State.CurrentState.Id || State.CurrentState.VotedFor != default(Guid))
            {
                return new RequestVoteResponse(false, State.CurrentState.CurrentTerm);
            }

            if (requestVoteRpc.LastLogIndex == _log.LastLogIndex &&
                requestVoteRpc.LastLogTerm == _log.LastLogTerm)
            {
                //added this prematurely?
                State = State.Handle(requestVoteRpc);
                return new RequestVoteResponse(true, State.CurrentState.CurrentTerm);
            }

            return new RequestVoteResponse(false, State.CurrentState.CurrentTerm);
        }

        private void Handle(BeginElection beginElection)
        {
            State = State.Handle(beginElection);
        }

        private void Handle(Timeout timeout)
        {
            if (NoHeartbeatSinceLastTimeout())
            {
                State = State.Handle(timeout);

                if (State is Candidate)
                {
                    //generate a random delay so that not all elections start at once
                    //this helps get a result..
                    var beginElectionDelay = _random.Get(100,
                        Convert.ToInt32(State.CurrentState.Timeout.TotalMilliseconds));
                    _sendToSelf.Publish(new BeginElection(beginElectionDelay));
                }
            }
            else
            {
                //var timeoutDelay = _random.Get(300, Convert.ToInt32(State.CurrentState.Timeout.TotalMilliseconds));
                //_sendToSelf.Publish(new Timeout(timeoutDelay));
            }

            if (AppendEntriesReceived())
            {
                _appendEntriesAtPreviousHeartbeat = _appendEntriesIdsReceived.Last();
            }

        }

        private bool AppendEntriesReceived()
        {
            return _appendEntriesIdsReceived.Any();
        }

        private bool NoHeartbeatSinceLastTimeout()
        {
            if (!_appendEntriesIdsReceived.Any())
            {
                return true;
            }

            return _appendEntriesIdsReceived.Last() == _appendEntriesAtPreviousHeartbeat;
        }#1#
    }
}*/

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

        public Node(IFiniteStateMachine fsm, ILog log, List<IPeer> peers, IRandomDelay random, Settings settings)
        {
            _fsm = fsm;
            _log = log;
            _peers = peers;
            _random = random;
            BecomeFollower(new CurrentState(Guid.NewGuid(), 0, default(Guid), -1, -1, settings.MinTimeout, settings.MaxTimeout));
        }

        public IState State { get; private set; }

        public void BecomeCandidate(CurrentState state)
        {
            State.Stop();
            var candidate = new Candidate(state, _fsm, _peers, _log, _random, this);
            State = candidate;
            candidate.BeginElection();
        }

        public void BecomeLeader(CurrentState state)
        {
        }

        public void BecomeFollower(CurrentState state)
        {
            State.Stop();
            State = new Follower(state, _fsm, _log, _random, this);
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