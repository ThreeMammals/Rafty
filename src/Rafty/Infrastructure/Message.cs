namespace Rafty.Infrastructure
{
    using System;

    public abstract class Message
    {
        public Message(Guid messageId)
        {
            MessageId = messageId;
        }

        public Guid MessageId { get; private set; }
    }
}