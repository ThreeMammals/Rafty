using System;

namespace Rafty.Concensus
{
    public class Timeout
    {
        public Timeout(Guid messageId, TimeSpan delay)
        {
            this.Delay = delay;
            this.MessageId = messageId;

        }

        public TimeSpan Delay { get; private set; }
        public Guid MessageId { get; private set; }
    }
}