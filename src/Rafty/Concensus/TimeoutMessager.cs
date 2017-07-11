using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rafty.Concensus
{
    public class TimeoutMessager : IDisposable
    {
        private Task _publishingTask;
        private readonly BlockingCollection<Message> _messages;
        private readonly List<Guid> _seenMessageIds;
        private bool _publishing;
        private readonly CancellationTokenSource _messagesCancellationTokenSource;
        private readonly CancellationTokenSource _taskCancellationTokenSource;

        private Node _node;

        public TimeoutMessager(Node state)
        {
            _node = state;
            _seenMessageIds = new List<Guid>();
            _messages = new BlockingCollection<Message>();
            _taskCancellationTokenSource = new CancellationTokenSource();
            _messagesCancellationTokenSource = new CancellationTokenSource();
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
            _publishingTask = new Task(Process, _taskCancellationTokenSource.Token);
            _publishingTask.Start();
            _publishing = true;
        }

        private void Process()
        {

            while (_publishing)
            {
                try
                {
                    foreach (var message in _messages.GetConsumingEnumerable(_messagesCancellationTokenSource.Token))
                    {
                        if (_seenMessageIds.Contains(message.MessageId))
                        {
                            return;
                        }

                        //todo - this is wrong as it blocks messages that need to be processed
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
            _taskCancellationTokenSource.Cancel(true);
            _messagesCancellationTokenSource.Cancel(true);
        }
    }
}