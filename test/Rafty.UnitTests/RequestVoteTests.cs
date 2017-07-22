using System;
using System.Collections.Generic;
using System.Text;
using Rafty.Concensus;
using Rafty.Log;

namespace Rafty.UnitTests
{
    public class RequestVoteTests : IDisposable
    {
/*
1. Reply false if term<currentTerm (§5.1)
2. If votedFor is null or candidateId, and candidate’s log is at
least as up-to-date as receiver’s log, grant vote(§5.2, §5.4)
*/
        private Node _node;
        private ISendToSelf _sendToSelf;
        private CurrentState _currentState;

        public RequestVoteTests()
        {
            _sendToSelf = new TestingSendToSelf();
            _currentState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid), TimeSpan.FromSeconds(5), new InMemoryLog(), 0);
            _node = new Node(_currentState, _sendToSelf);
            _sendToSelf.SetNode(_node);
        }

        public void Dispose()
        {
            _node.Dispose();
        }
    }
}
