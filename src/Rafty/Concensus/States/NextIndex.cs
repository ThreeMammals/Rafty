namespace Rafty.Concensus
{
    public class NextIndex
    {
        public NextIndex(IPeer peer, int nextLogIndexToSendToPeer)
        {
            Peer = peer;
            NextLogIndexToSendToPeer = nextLogIndexToSendToPeer;
        }

        public IPeer Peer { get; private set; }
        public int NextLogIndexToSendToPeer { get; private set; }
    }
}