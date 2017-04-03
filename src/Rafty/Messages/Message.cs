using System;

namespace Rafty.Messages
{
    public class Message : IMessage
    {
        protected Message()
        {
            MessageId = Guid.NewGuid();
        }

        public Guid MessageId { get; }
    }
}