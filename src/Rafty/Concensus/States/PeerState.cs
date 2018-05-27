namespace Rafty.Concensus.States
{
    using Peers;

    public class PeerState
    {
        public PeerState(IPeer peer, MatchIndex matchIndex, NextIndex nextIndex)
        {
            Peer = peer;
            MatchIndex = matchIndex;
            NextIndex = nextIndex;
        }

        public IPeer Peer { get; private set; }
        public MatchIndex MatchIndex { get; private set; }
        public NextIndex NextIndex { get; private set; }

        public void UpdateMatchIndex(int indexOfHighestKnownReplicatedLog)
        {
            MatchIndex = new MatchIndex(Peer, indexOfHighestKnownReplicatedLog);
        }

        public void UpdateNextIndex(int nextLogIndexToSendToPeer)
        {
            NextIndex = new NextIndex(Peer, nextLogIndexToSendToPeer);
        }
    }
}