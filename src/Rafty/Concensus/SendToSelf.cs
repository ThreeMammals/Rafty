using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rafty.Concensus
{
    public class SendToSelf : IDisposable
    {
        private Thread _publishingThread;
        private readonly BlockingCollection<Message> _messages;
        private readonly List<Guid> _seenMessageIds;
        private bool _publishing;
        private readonly CancellationTokenSource _messagesCancellationTokenSource;
        private readonly CancellationTokenSource _taskCancellationTokenSource;
        private readonly INode _node;
        private readonly List<TaskAndCancellationToken> _messagesBeingProcessed;

        public SendToSelf(INode state)
        {
            _node = state;
            _seenMessageIds = new List<Guid>();
            _messages = new BlockingCollection<Message>();
            _taskCancellationTokenSource = new CancellationTokenSource();
            _messagesCancellationTokenSource = new CancellationTokenSource();
            _messagesBeingProcessed = new List<TaskAndCancellationToken>();
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
                    foreach (var message in _messages.GetConsumingEnumerable(_messagesCancellationTokenSource.Token))
                    {
                        if (_seenMessageIds.Contains(message.MessageId))
                        {
                            return;
                        }

                        var cancel = new CancellationTokenSource();
                        Action action = () => Process(message);
                        var task = new Task(action, cancel.Token);
                        task.Start();
                        var taskAndCancel = new TaskAndCancellationToken(task, cancel);
                        _messagesBeingProcessed.Add(taskAndCancel);
                        _seenMessageIds.Add(message.MessageId);
                    }
                }
                catch (OperationCanceledException exception)
                {
                    //blocking cancelled ignore for now...
                }
            }
        }

        private async Task Process(Message message)
        {
            await Task.Delay(message.Delay);
            _node.Handle(message);
        }

        public void Dispose()
        {
            _publishing = false;

            foreach (var taskAndCancellationToken in _messagesBeingProcessed)
            {
                taskAndCancellationToken.CancellationTokenSource.Cancel(true);
            }

            _taskCancellationTokenSource.Cancel(true);
            _messagesCancellationTokenSource.Cancel(true);
        }
    }
}