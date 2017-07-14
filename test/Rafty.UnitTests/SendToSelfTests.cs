namespace Rafty.UnitTests
{
    using System;
    using System.Threading.Tasks;
    using Concensus;
    using Shouldly;
    using Xunit;

    public class SendToSelfTests : IDisposable
    {
        public SendToSelfTests()
        {
            _node = new FakeNode();
            _sendToSelf = new SendToSelf();
            _sendToSelf.SetNode(_node);
        }

        public void Dispose()
        {
            _sendToSelf?.Dispose();
        }

        private readonly FakeNode _node;
        private readonly SendToSelf _sendToSelf;

        [Fact]
        public async Task ShouldReceiveDelayedMessagesInCorrectOrder()
        {
            var oneSecond = new Timeout(TimeSpan.FromMilliseconds(100));
            var halfASecond = new Timeout(TimeSpan.FromMilliseconds(50));
            _sendToSelf.Publish(oneSecond);
            _sendToSelf.Publish(halfASecond);
            //This is a bit shitty but probably the best way to test the 
            //integration between this and the INode
            await Task.Delay(500);
            _node.Messages[0].MessageId.ShouldBe(halfASecond.MessageId);
            _node.Messages[1].MessageId.ShouldBe(oneSecond.MessageId);
        }
    }
}