using Rafty.FiniteStateMachine;

namespace Rafty.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Concensus;
    using Concensus.Messages;
    using Concensus.Peers;
    using Infrastructure;

    internal class FakePeer : IPeer
    {
        private readonly bool _grantVote;
        private readonly bool _appendEntry;
        private readonly bool _appendEntryTwo;
        private bool _appendEntryThree;
        private int _term = 1;
        private string _id;
        public int ReceivedCommands;

        public FakePeer()
        {
        }

        public FakePeer(string id)
        {
            _id = id;
        }

        public FakePeer(int term)
        {
            _term = term;
        }

        public FakePeer(int term, string id)
        {
            _term = term;
            _id = id;
        }

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

        public string Id => _id;

        public async Task<RequestVoteResponse> Request(RequestVote requestVote)
        {
            var response = new RequestVoteResponse(_grantVote, _term);
            RequestVoteResponses.Add(response);
            return response;
        }

        public async Task<AppendEntriesResponse> Request(AppendEntries appendEntries)
        {
            AppendEntriesResponse response;
            if (AppendEntriesResponses.Count == 1)
            {
                response = new AppendEntriesResponse(_term, _appendEntryTwo);
                AppendEntriesResponses.Add(response);
                return response;
            }

            if (AppendEntriesResponses.Count == 2)
            {
                response = new AppendEntriesResponse(_term, _appendEntryThree);
                AppendEntriesResponses.Add(response);
                return response;
            }

            response = new AppendEntriesResponse(_term, _appendEntry);
            AppendEntriesResponses.Add(response);
            return response;
        }

        public async Task<Response<T>> Request<T>(T command) where T : ICommand
        {
            ReceivedCommands++;
            return new OkResponse<T>(command);
        }
    }
}