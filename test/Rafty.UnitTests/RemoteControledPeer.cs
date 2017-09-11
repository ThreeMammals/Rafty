using System;
using Rafty.Concensus;

namespace Rafty.UnitTests
{
    public class RemoteControledPeer : IPeer
    {
        private RequestVoteResponse _requestVoteResponse;
        private AppendEntriesResponse _appendEntriesResponse;
        public int RequestVoteResponses { get; private set; }
        public int AppendEntriesResponses { get; private set; }

        public RemoteControledPeer()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }

        public void SetRequestVoteResponse(RequestVoteResponse requestVoteResponse)
        {
            _requestVoteResponse = requestVoteResponse;
        }

        public void SetAppendEntriesResponse(AppendEntriesResponse appendEntriesResponse)
        {
            _appendEntriesResponse = appendEntriesResponse;
        }

        public RequestVoteResponse Request(RequestVote requestVote)
        {
            RequestVoteResponses++;
            return _requestVoteResponse;
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            AppendEntriesResponses++;
            return _appendEntriesResponse;
        }

        public Response<T> Request<T>(T command)
        {
            throw new NotImplementedException();
        }
    }
}