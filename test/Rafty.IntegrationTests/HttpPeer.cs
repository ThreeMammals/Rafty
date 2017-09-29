using System;
using System.Net.Http;
using Newtonsoft.Json;
using Rafty.Concensus;

namespace Rafty.IntegrationTests
{
    public class HttpPeer : IPeer
    {
        private string _hostAndPort;
        private HttpClient _httpClient;
        public HttpPeer(string hostAndPort, Guid id, HttpClient httpClient)
        {
            Id  = id;
            _hostAndPort = hostAndPort;
            _httpClient = httpClient;
        }

        public Guid Id {get; private set;}

        public RequestVoteResponse Request(RequestVote requestVote)
        {
            var json = JsonConvert.SerializeObject(requestVote);
            var content = new StringContent(json);
            var response = _httpClient.PostAsync($"{_hostAndPort}/requestvote", content).GetAwaiter().GetResult();
            if(response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<RequestVoteResponse>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            else
            {
                return new RequestVoteResponse(false, requestVote.Term);
            }
        }

        public AppendEntriesResponse Request(AppendEntries appendEntries)
        {
            try
            {
                var json = JsonConvert.SerializeObject(appendEntries);
                var content = new StringContent(json);
                var response = _httpClient.PostAsync($"{_hostAndPort}/appendEntries", content).GetAwaiter().GetResult();
                if(response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<AppendEntriesResponse>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                }
                else
                {
                    return new AppendEntriesResponse(appendEntries.Term, false);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return new AppendEntriesResponse(appendEntries.Term, false);
            }
        }

        public Response<T> Request<T>(T command)
        {
            var json = JsonConvert.SerializeObject(command);
            var content = new StringContent(json);
            var response = _httpClient.PostAsync($"{_hostAndPort}/command", content).GetAwaiter().GetResult();
            if(response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<OkResponse<T>>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
            else 
            {
                return new ErrorResponse<T>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), command);
            }
        }
    }
}
