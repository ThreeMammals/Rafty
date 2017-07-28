namespace Rafty.UnitTests
{
    using System.Collections.Generic;
    using Concensus;

    internal class FakePeer : IPeer
    {
        private readonly bool _grantVote;
        private readonly bool _appendEntry;
        private readonly bool _appendEntryTwo;
        private bool _appendEntryThree;

        public FakePeer(bool grantVote)
        {
            _grantVote = grantVote;
        }

        public FakePeer(bool grantVote, bool appendEntry)
        {
            _grantVote = grantVote;
            _appendEntry = appendEntry;
        }

        public FakePeer(bool grantVote, bool appendEntry, bool appendEntryTwo)
        {
            _grantVote = grantVote;
            _appendEntry = appendEntry;
            _appendEntryTwo = appendEntryTwo;
        }

        public FakePeer(bool grantVote, bool appendEntry, bool appendEntryTwo, bool appendEntryThree)
        {
            _grantVote = grantVote;
            _appendEntry = appendEntry;
            _appendEntryTwo = appendEntryTwo;
            _appendEntryThree = appendEntryThree;
        }

        public List<RequestVoteResponse> RequestVoteResponses { get; } = new List<RequestVoteResponse>();

        public List<AppendEntriesResponse> AppendEntriesResponses { get; } = new List<AppendEntriesResponse>();

        public RequestVoteResponse Request(RequestVote requestVote)
        {
            var response = new RequestVoteResponse(_grantVote, 1);
            RequestVoteResponses.Add(response);
            return response;
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            AppendEntriesResponse response;
            if (AppendEntriesResponses.Count == 1)
            {
                response = new AppendEntriesResponse(1, _appendEntryTwo);
                AppendEntriesResponses.Add(response);
                return response;
            }

            if (AppendEntriesResponses.Count == 2)
            {
                response = new AppendEntriesResponse(1, _appendEntryThree);
                AppendEntriesResponses.Add(response);
                return response;
            }

            response = new AppendEntriesResponse(1, _appendEntry);
            AppendEntriesResponses.Add(response);
            return response;
        }
    }
}