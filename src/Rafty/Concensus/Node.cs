using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rafty.Concensus
{ 
    public class Node : IDisposable, INode
    {
        private readonly List<Guid> _appendEntriesIdsReceived;
        private Guid _appendEntriesAtPreviousHeartbeat;
        private readonly ISendToSelf _sendToSelf;

        public Node(CurrentState initialState, ISendToSelf sendToSelf)
        {
            _appendEntriesIdsReceived = new List<Guid>();
            _sendToSelf = sendToSelf;
            State = new Follower(initialState, _sendToSelf);
        }

        public IState State { get; private set; }

        public void Handle(Message message)
        {
            //todo - could run middleware type functions here?
            //todo - these handlers should be in a dictionary
            if(message.GetType() == typeof(BeginElection))
            {
                Handle((BeginElection)message);
            }

            if(message.GetType() == typeof(Timeout))
            {
                Handle((Timeout)message);
            }
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            _appendEntriesIdsReceived.Add(appendEntries.MessageId);
            return new AppendEntriesResponse();
        }

        public void Dispose()
        {
            _sendToSelf.Dispose();
        }

        private void Handle(BeginElection beginElection)
        {
            State = State.Handle(beginElection);

            _sendToSelf.Publish(new Timeout(State.CurrentState.Timeout));
        }

        private void Handle(Timeout timeout)
        {
            if(NoHeartbeatSinceLastTimeout())
            {
                State = State.Handle(timeout);
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
            if(!_appendEntriesIdsReceived.Any())
            {
                return true;
            }

            return _appendEntriesIdsReceived.Last() == _appendEntriesAtPreviousHeartbeat;
        }
    }
}