using System;

namespace Rafty
{
    public class Next
    {
        public Next(Guid id, int nextIndex)
        {
            this.Id = id;
            this.NextIndex = nextIndex;

        }
        public Guid Id { get; private set; }
        public int NextIndex { get; private set; }
    }
}