using System;

namespace Rafty
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