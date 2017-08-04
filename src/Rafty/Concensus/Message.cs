namespace Rafty.Concensus
{
    using System;
    
    public interface IRandomDelay
    {
        TimeSpan Get(int leastMilliseconds, int maxMilliseconds);
    }

    public class RandomDelay : IRandomDelay
    {
        private Random _random;

        public RandomDelay()
        {
            _random = new Random(Guid.NewGuid().GetHashCode());
        }
        public TimeSpan Get(int leastMilliseconds, int maxMilliseconds)
        {
            var randomMs = _random.Next(leastMilliseconds, maxMilliseconds);
            return TimeSpan.FromMilliseconds(randomMs);       
        }
    }
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
            /*var random = new Random(Guid.NewGuid().GetHashCode());
            var currentMs = Convert.ToInt32(delay.TotalMilliseconds);
            //todo get from config
            var randomMs = random.Next(100, currentMs + currentMs);*/
            Delay = delay;
        }

        public Guid MessageId { get; private set; }
        public TimeSpan Delay { get; private set; }
    }
}