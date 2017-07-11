using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Rafty.Concensus
{
    public class TimeoutMessager : IDisposable
    {
        private Thread _publishingThread;
        private readonly BlockingCollection<Message> _messages;
        private readonly List<Guid> _seenMessageIds;
        private bool _publishing;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Node _node;

        public TimeoutMessager(Node state)
        {
            _node = state;
            _seenMessageIds = new List<Guid>();
            _messages = new BlockingCollection<Message>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Publish(Timeout timeout)
        {
            _messages.Add(timeout);
        }

        public void Publish(BeginElection beginElection)
        {
            _messages.Add(beginElection);
        }

        public void Start()
        {
            _publishingThread = new Thread(Process);
            _publishingThread.Start();
            _publishing = true;
        }

        private void Process()
        {

            while (_publishing)
            {
                try
                {
                    foreach (var message in _messages.GetConsumingEnumerable(_cancellationTokenSource.Token))
                    {
                        if (_seenMessageIds.Contains(message.MessageId))
                        {
                            return;
                        }

                        Thread.Sleep(message.Delay);
                        _node.Handle(message);
                        _seenMessageIds.Add(message.MessageId);
                    }
                }
                catch (OperationCanceledException exception)
                {
                    //blocking cancelled ignore for now...
                }
            }
        }

        public void Dispose()
        {
            _publishing = false;
            _cancellationTokenSource.Cancel(true);
        }
    }
}