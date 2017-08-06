namespace Rafty.Concensus
{
    /// <summary>
    /// for each server, index of highest log entry known to be replicated on server
    /// </summary>
    public class MatchIndex
    {
        public MatchIndex(IPeer peer, int indexOfHighestKnownReplicatedLog)
        {
            Peer = peer;
            IndexOfHighestKnownReplicatedLog = indexOfHighestKnownReplicatedLog;
        }

        public IPeer Peer { get; private set; }
        public int IndexOfHighestKnownReplicatedLog { get; private set; }
    }
}