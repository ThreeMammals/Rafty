using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rafty.Concensus
{
    public class VoteForSelf
    {

    }
    
    public class Node : IDisposable
    {
        private List<Guid> _appendEntriesIdsReceived;
        private Guid _appendEntriesAtPreviousHeartbeat;
        private TimeoutMessager _bus;

        public Node(CurrentState initialState)
        {
            _appendEntriesIdsReceived = new List<Guid>();
            _bus = new TimeoutMessager(this);
            State = new Follower(initialState, _bus);
            _bus.Start();
        }

        public IState State { get; private set; }

        public void Handle(Message message)
        {
            if(message.GetType() == typeof(BeginElection))
            {
                Handle((BeginElection)message);
            }

            if(message.GetType() == typeof(Timeout))
            {
                Handle((Timeout)message);
            }
        }

        private void Handle(BeginElection beginElection)
        {
            // • On conversion to candidate, start election:
            // • Increment currentTerm
            // • Vote for self
            State = State.Handle(new VoteForSelf());
            // • Reset election timer
            // • Send RequestVote RPCs to all other servers
        }

        private void Handle(Timeout timeout)
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

        public void Dispose()
        {
            _bus.Dispose();
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