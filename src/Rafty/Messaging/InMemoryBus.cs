using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rafty.Commands;
using Rafty.Infrastructure;
using Rafty.Messages;
using Rafty.Responses;

namespace Rafty.Messaging
{
    public class InMemoryBus : IMessageBus, IReportable, IDisposable
    {
        private readonly IMessageSender _messageSender;
        private readonly Thread _publishingThread;
        private readonly BlockingCollection<SendToSelf> _messages;
        private readonly List<Guid> _seenMessages;
        private bool _publishing;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public InMemoryBus(IMessageSender messageSender)
        {
            _seenMessages = new List<Guid>();
            _messages = new BlockingCollection<SendToSelf>();
            _messageSender = messageSender;
            _cancellationTokenSource = new CancellationTokenSource();
            _publishingThread = new Thread(Start);
            _publishingThread.Start();
        }

        public void Publish(SendToSelf message)
        {
            _messages.Add(message);
        }

        private void Start()
        {
            _publishing = true;

            while(_publishing)
            {
                try
                {
                    foreach (var message in _messages.GetConsumingEnumerable(_cancellationTokenSource.Token))
                    {
                        if (_seenMessages.Contains(message.MessageId))
                        {
                            return;
                        }

                        _messageSender.Send(message);
                        _seenMessages.Add(message.MessageId);
                    }
                }
                catch (OperationCanceledException exception)
                {
                    //blocking cancelled..
                }
            }
        }

        public string Name => "InMemoryBus";

        public int Count => _messages.Count;

        public async Task<AppendEntriesResponse> Send(AppendEntries appendEntries)
        {
            return await _messageSender.Send(appendEntries);
        }

        public async Task<RequestVoteResponse> Send(RequestVote requestVote)
        {
            return await _messageSender.Send(requestVote);
        }

        public async Task<SendLeaderCommandResponse> Send(ICommand command, Guid leaderId)
        {
            return await _messageSender.Send(command, leaderId);
        }

        public void Dispose()
        {
            _publishing = false;
            _cancellationTokenSource.Cancel(true);
        }
    }
}