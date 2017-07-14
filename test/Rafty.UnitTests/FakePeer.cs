namespace Rafty.UnitTests
{
    using System.Collections.Generic;
    using Concensus;

    internal class FakePeer : IPeer
    {
        public List<RequestVoteResponse> RequestVoteResponses { get; private set; } = new List<RequestVoteResponse>();

        public List<AppendEntriesResponse> AppendEntriesResponses { get; private set; } = new List<AppendEntriesResponse>();

        public RequestVoteResponse Request(RequestVote requestVote)
        {
            var response =  new RequestVoteResponse();
            RequestVoteResponses.Add(response);
            return response;
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            var response = new AppendEntriesResponse();
            AppendEntriesResponses.Add(response);
            return response;
        }

        public static List<IPeer> Build(int count)
        {
            var peers = new List<IPeer>();
            for (int i = 0; i < count; i++)
            {
                peers.Add(new FakePeer());
            }
            return peers;
        } 
    }
}