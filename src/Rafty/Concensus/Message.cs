namespace Rafty.Concensus
{
    using System;

    public abstract class Message
    {
        public Message(Guid messageId)
        {
            MessageId = messageId;
            Delay = TimeSpan.FromMilliseconds(0);
        }

        public Message(Guid messageId, TimeSpan delay)
        {
            MessageId = messageId;
            Delay = delay;
        }

        public Guid MessageId { get; private set; }
        public TimeSpan Delay { get; private set; }
    }
}