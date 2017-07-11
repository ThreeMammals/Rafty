using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Castle.Components.DictionaryAdapter;
using Rafty.Concensus;
using Shouldly;
using Xunit;
using Timeout = Rafty.Concensus.Timeout;

namespace Rafty.UnitTests
{
    internal class FakeNode : INode
    {
        public FakeNode()
        {
            Messages = new List<Message>();
        }

        public List<Message> Messages { get; private set; }

        public IState State { get; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public AppendEntriesResponse Handle(AppendEntries appendEntries)
        {
            throw new NotImplementedException();
        }

        public void Handle(Message message)
        {
            Messages.Add(message);
        }
    }

    public class SendToSelfTests : IDisposable
    {
        private readonly FakeNode _node;
        private readonly SendToSelf _sendToSelf;

        public SendToSelfTests()
        {
            _node = new FakeNode();
            _sendToSelf = new SendToSelf(_node);
            _sendToSelf.Start();
        }

        [Fact]
        public async Task Test()
        {
            var oneSecond = new Timeout(TimeSpan.FromMilliseconds(1000));
            var halfASecond = new Timeout(TimeSpan.FromMilliseconds(500));
            _sendToSelf.Publish(oneSecond);
            _sendToSelf.Publish(halfASecond);
            await Task.Delay(5000);
            _node.Messages[0].MessageId.ShouldBe(halfASecond.MessageId);
            _node.Messages[1].MessageId.ShouldBe(oneSecond.MessageId);
        }

        public void Dispose()
        {
            _sendToSelf?.Dispose();
        }
    }
}
