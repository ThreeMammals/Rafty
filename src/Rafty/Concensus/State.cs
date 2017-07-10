using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rafty.Concensus
{
    public interface IState
    {
        IState Handle(Timeout timeout);
        CurrentState CurrentState {get;}
    }

    public class CurrentState
    {
        public CurrentState(Guid id, List<IPeer> peers)
        {
            this.Id = id;

        }

        public long CurrentTerm { get; private set; }
        public Guid VotedFor { get; private set; }
        public long CommitIndex { get; private set; }
        public long LastApplied { get; private set; }
        public Uri Address { get; private set; }
        public Guid Id { get; private set; }
        public List<IPeer> Peers { get; private set; }
    }

    public class Node
    {
        private List<Guid> _appendEntriesIdsReceived;
        private Guid _previousAppendEntriesId;

        public Node(CurrentState initialState)
        {
            _appendEntriesIdsReceived = new List<Guid>();
            State = new Follower(initialState);
        }

        public IState State { get; private set; }

        public AppendEntriesResponse Response(AppendEntries appendEntries)
        {
            throw new NotImplementedException();
        }

        public RequestVoteResponse Response(RequestVote requestVote)
        {
            throw new NotImplementedException();
        }

        public void Handle(Timeout timeout)
        {
            if(NoHeartbeatSinceLastTimeout())
            {
                State = State.Handle(timeout);
            }

            if(_appendEntriesIdsReceived.Any())
            {
                _previousAppendEntriesId = _appendEntriesIdsReceived.Last();
            }
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            _appendEntriesIdsReceived.Add(appendEntries.Id);
            return new AppendEntriesResponse();
        }

        private bool NoHeartbeatSinceLastTimeout()
        {
            if(!_appendEntriesIdsReceived.Any())
            {
                return true;
            }

            return _appendEntriesIdsReceived.Last() == _previousAppendEntriesId;
        }
    }
}