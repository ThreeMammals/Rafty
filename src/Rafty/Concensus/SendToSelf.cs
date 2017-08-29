/*namespace Rafty.Concensus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class SendToSelf : ISendToSelf, IDisposable
    {
        private readonly BlockingCollection<Message> _messages;
        private readonly List<TaskAndCancellationToken> _messagesBeingProcessed;
        private readonly CancellationTokenSource _messagesCancellationTokenSource;
        private readonly List<Guid> _seenMessageIds;
        private readonly CancellationTokenSource _taskCancellationTokenSource;
        private bool _disposing;
        private INode _node;
        private bool _publishing;
        private Thread _publishingThread;

        public SendToSelf()
        {
            _disposing = false;
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

        public void SetNode(INode node)
        {
            _node = node;
            _publishingThread = new Thread(Process);
            _publishingThread.Start();
            _publishing = true;
        }

        public void Dispose()
        {
            _disposing = true;
            while (_disposing)
            {
                try
                {
                    _publishing = false;
                    foreach (var taskAndCancellationToken in _messagesBeingProcessed)
                    {
                        taskAndCancellationToken.CancellationTokenSource.Cancel(true);
                    }
                    _taskCancellationTokenSource.Cancel(true);
                    _messagesCancellationTokenSource.Cancel(true);
                    _disposing = false;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
         
        }

        private void Process()
        {
            if (_disposing)
            {
                return;
            }

            while (_publishing)
            {
                try
                {
                    _messagesBeingProcessed.RemoveAll(x => x.Task.IsCompleted);

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

        public void Restart()
        {
            _publishing = true;
        }

        private async Task Process(Message message)
        {
            await Task.Delay(message.Delay);
            _node.Handle(message);
        }
    }
}*/