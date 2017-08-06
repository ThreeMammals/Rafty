namespace Rafty.Concensus
{
    /// <summary>
    /// for each server, index of the next log entry to send to that server
    /// </summary>
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