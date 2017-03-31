namespace Rafty
{
    using System;

    public interface IMessage
    {
        Guid MessageId { get; }
    }
}