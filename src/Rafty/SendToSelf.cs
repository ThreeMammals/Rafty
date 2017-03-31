namespace Rafty
{
    public class SendToSelf : Message
    {
        public SendToSelf(IMessage message)
        {
            Message = message;
        }

        public SendToSelf(IMessage message, int delaySeconds)
        {
            Message = message;
            DelaySeconds = delaySeconds;
        }

        public IMessage Message { get; private set; }
        public int DelaySeconds { get; private set; }
    }
}