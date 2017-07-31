namespace Rafty.AcceptanceTests
{
    using TestStack.BDDfy;
    using Shouldly;
    using Xunit;
    using Rafty.Concensus;
    using System;
    using System.Collections.Generic;
    using Rafty.Log;
    using System.Threading;

    public class Tests : IDisposable
    {
        private InMemoryLog _log;
        private SendToSelf _sendToSelf;
        private InMemoryStateMachine _fsm;
        private CurrentState _initialState;
        private Node _node;

        [Fact]
        public void NodeStarts()
        {
            var thread = new Thread(StartNode);
            thread.Start();
            Thread.Sleep(1000);
            var term = _node.State.CurrentState.CurrentTerm;
        }

        public void Dispose()
        {
            _node.Dispose();
        }

        private void StartNode()
        {
            _log = new InMemoryLog();
            _sendToSelf = new SendToSelf();
            _fsm = new InMemoryStateMachine();
            _initialState = new CurrentState(Guid.NewGuid(), new List<IPeer>(), 0, default(Guid), TimeSpan.FromMilliseconds(100), _log, -1, -1);
            _node = new Node(_initialState, _sendToSelf, _fsm);
            _sendToSelf.SetNode(_node);
        }
    }
}