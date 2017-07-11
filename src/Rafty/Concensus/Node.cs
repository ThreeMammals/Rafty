using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rafty.Concensus
{
    public class Node
    {
        private List<Guid> _appendEntriesIdsReceived;
        private Guid _appendEntriesAtPreviousHeartbeat;

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

            if(AppendEntriesReceived())
            {
                _appendEntriesAtPreviousHeartbeat = _appendEntriesIdsReceived.Last();
            }
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            _appendEntriesIdsReceived.Add(appendEntries.MessageId);
            return new AppendEntriesResponse();
        }

        private bool AppendEntriesReceived()
        {
            return _appendEntriesIdsReceived.Any();
        }

        private bool NoHeartbeatSinceLastTimeout()
        {
            if(!_appendEntriesIdsReceived.Any())
            {
                return true;
            }

            return _appendEntriesIdsReceived.Last() == _appendEntriesAtPreviousHeartbeat;
        }
    }
}