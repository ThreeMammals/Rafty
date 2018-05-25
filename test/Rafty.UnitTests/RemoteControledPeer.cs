using System;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;

namespace Rafty.UnitTests
{
    using System.Threading.Tasks;

    public class RemoteControledPeer : IPeer
    {
        private RequestVoteResponse _requestVoteResponse;
        private AppendEntriesResponse _appendEntriesResponse;
        public int RequestVoteResponses { get; private set; }
        public int AppendEntriesResponses { get; private set; }
        public int AppendEntriesResponsesWithLogEntries {get;private set;}

        public RemoteControledPeer()
        {
            Id = Guid.NewGuid().ToString();
        }

        public string Id { get; }

        public void SetRequestVoteResponse(RequestVoteResponse requestVoteResponse)
        {
            _requestVoteResponse = requestVoteResponse;
        }

        public void SetAppendEntriesResponse(AppendEntriesResponse appendEntriesResponse)
        {
            _appendEntriesResponse = appendEntriesResponse;
        }

        public async Task<RequestVoteResponse> Request(RequestVote requestVote)
        {
            RequestVoteResponses++;
            return _requestVoteResponse;
        }

        public async Task<AppendEntriesResponse> Request(AppendEntries appendEntries)
        {
            if(appendEntries.Entries.Count > 0)
            {
                AppendEntriesResponsesWithLogEntries++;
            }
            AppendEntriesResponses++;
            return _appendEntriesResponse;
        }

        public async Task<Response<T>> Request<T>(T command) where T : ICommand
        {
            throw new NotImplementedException();
        }
    }
}