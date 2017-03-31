using System;

namespace Rafty.Messages
{
    public interface IMessage
    {
        Guid MessageId { get; }
    }
}