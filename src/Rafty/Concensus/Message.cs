using System;

namespace Rafty.Concensus
{
    public abstract class Message
    {
        public Message(Guid messageId)
        {
            MessageId = messageId;
        }

        public Guid MessageId {get;private set;}
    }
}