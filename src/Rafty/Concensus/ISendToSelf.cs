namespace Rafty.Concensus
{
    //This class lets the injected node send messages to itself
    public interface ISendToSelf
    {
        //sets the node we will send messages to
        void SetNode(INode node);
        void Publish(Timeout timeout);
        void Publish(BeginElection beginElection);
        //needs to be disposed as has some long running processes
        void Dispose();
    }
}