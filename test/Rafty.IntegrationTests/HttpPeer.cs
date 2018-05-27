using System;
using System.Net.Http;
using Newtonsoft.Json;
using Rafty.Concensus;
using Rafty.FiniteStateMachine;

namespace Rafty.IntegrationTests
{
    using System.Threading.Tasks;
    using Concensus.Messages;
    using Concensus.Peers;
    using Infrastructure;

    public class HttpPeer : IPeer
    {
        private string _hostAndPort;
        private HttpClient _httpClient;
        private JsonSerializerSettings _jsonSerializerSettings;

        public HttpPeer(string hostAndPort, HttpClient httpClient)
        {
            Id  = hostAndPort;
            _hostAndPort = hostAndPort;
            _httpClient = httpClient;
            _jsonSerializerSettings = new JsonSerializerSettings() { 
                TypeNameHandling = TypeNameHandling.All
            };
        }

        public string Id {get; private set;}

        public async Task<RequestVoteResponse> Request(RequestVote requestVote)
        {
            var json = JsonConvert.SerializeObject(requestVote, _jsonSerializerSettings);
            var content = new StringContent(json);
            var response = await _httpClient.PostAsync($"{_hostAndPort}/requestvote", content);
            if(response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<RequestVoteResponse>(await response.Content.ReadAsStringAsync());
            }
            else
            {
                return new RequestVoteResponse(false, requestVote.Term);
            }
        }

        public async Task<AppendEntriesResponse> Request(AppendEntries appendEntries)
        {
            try
            {
                var json = JsonConvert.SerializeObject(appendEntries, _jsonSerializerSettings);
                var content = new StringContent(json);
                var response = await _httpClient.PostAsync($"{_hostAndPort}/appendEntries", content);
                if(response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<AppendEntriesResponse>(await response.Content.ReadAsStringAsync());
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

        public async Task<Response<T>> Request<T>(T command) where T : ICommand
        {
            var json = JsonConvert.SerializeObject(command, _jsonSerializerSettings);
            var content = new StringContent(json);
            var response = await _httpClient.PostAsync($"{_hostAndPort}/command", content);
            if(response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<OkResponse<T>>(await response.Content.ReadAsStringAsync());
            }
            else 
            {
                return new ErrorResponse<T>(await response.Content.ReadAsStringAsync(), command);
            }
        }
    }
}
