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
            var random = new Random();
            var currentMs = Convert.ToInt32(delay.TotalMilliseconds);
            var randomMs = random.Next(100, currentMs + currentMs);
            Delay = TimeSpan.FromMilliseconds(randomMs);
        }

        public Guid MessageId { get; private set; }
        public TimeSpan Delay { get; private set; }
    }
}