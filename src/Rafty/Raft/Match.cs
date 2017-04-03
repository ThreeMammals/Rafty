using System;

namespace Rafty.Raft
{
    public class Match
    {
        public Match(Guid id, int matchIndex)
        {
            this.Id = id;
            this.MatchIndex = matchIndex;

        }
        public Guid Id { get; private set; }
        public int MatchIndex { get; private set; }
    }
}