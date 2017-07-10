using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Rafty.Concensus
{
    public class TimeoutMessager : IDisposable
    {
        private readonly Thread _publishingThread;
        private readonly BlockingCollection<Timeout> _timeouts;
        private readonly List<Guid> _seenMessageIds;
        private bool _publishing;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Node _node;

        public TimeoutMessager(Node state)
        {
            _node = state;
            _seenMessageIds = new List<Guid>();
            _timeouts = new BlockingCollection<Timeout>();
            _cancellationTokenSource = new CancellationTokenSource();
            _publishingThread = new Thread(Start);
            _publishingThread.Start();
        }

        public void Publish(Timeout timeout)
        {
            _timeouts.Add(timeout);
        }

        private void Start()
        {
            _publishing = true;

            while (_publishing)
            {
                try
                {
                    foreach (var timeout in _timeouts.GetConsumingEnumerable(_cancellationTokenSource.Token))
                    {
                        if (_seenMessageIds.Contains(timeout.MessageId))
                        {
                            return;
                        }

                        Thread.Sleep(timeout.Delay);
                        _node.Handle(timeout);
                        _seenMessageIds.Add(timeout.MessageId);
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