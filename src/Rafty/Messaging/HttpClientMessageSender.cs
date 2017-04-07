using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rafty.Commands;
using Rafty.Infrastructure;
using Rafty.Messages;
using Rafty.Raft;
using Rafty.Responses;
using Rafty.ServiceDiscovery;

namespace Rafty.Messaging
{
    public class HttpClientMessageSender : IMessageSender, IDisposable
    {
        private readonly IServiceRegistry _serviceRegistry;
        private Server _server;
        private readonly Dictionary<Type, Action<IMessage>> _sendToSelfHandlers;
        private bool _stopSendingMessages;
        private bool _sleeping;
        private readonly string _appendEntriesUrl;
        private readonly string _requestVoteUrl;
        private readonly string _commandUrl;
        private readonly ILogger _logger;

        public HttpClientMessageSender(IServiceRegistry serviceRegistry, ILogger logger, string raftyBasePath = null)
        {
            var urlConfig = RaftyUrlConfig.Get(raftyBasePath);

            _appendEntriesUrl = urlConfig.appendEntriesUrl;
            _requestVoteUrl = urlConfig.requestVoteUrl;
            _commandUrl = urlConfig.commandUrl;
            _serviceRegistry = serviceRegistry;
            _logger = logger;
            _sendToSelfHandlers = new Dictionary<Type, Action<IMessage>>
            {
                {typeof(BecomeCandidate), x => _server.Receive((BecomeCandidate) x)},
                {typeof(SendHeartbeat), x => _server.Receive((SendHeartbeat) x)},
                {typeof(Command), async x => await _server.Receive((Command) x)},
            };
        }

        public async Task<AppendEntriesResponse> Send(AppendEntries appendEntries)
        {
            try
            {
                var serverToSendMessageTo = _serviceRegistry.Get(RaftyServiceDiscoveryName.Get()).First(x => x.Id == appendEntries.FollowerId);
                var json = JsonConvert.SerializeObject(appendEntries, Formatting.None, new JsonSerializerSettings
                {
                    TypeNameHandling= TypeNameHandling.All
                });
                var httpContent = new StringContent(json);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = serverToSendMessageTo.Location;
                    var response = await httpClient.PostAsync(_appendEntriesUrl, httpContent);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var appendEntriesResponse = JsonConvert.DeserializeObject<AppendEntriesResponse>(content);
                    return appendEntriesResponse;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(new EventId(1), exception, "Error in Send(AppendEntries appendEntries)");
                throw;
            }
        }

        public async Task<RequestVoteResponse> Send(RequestVote requestVote)
        {
            try
            {
                var serverToSendMessageTo = _serviceRegistry.Get(RaftyServiceDiscoveryName.Get()).First(x => x.Id == requestVote.VoterId);
                var json = JsonConvert.SerializeObject(requestVote, Formatting.None, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
                var httpContent = new StringContent(json);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = serverToSendMessageTo.Location;
                    var response = await httpClient.PostAsync(_requestVoteUrl, httpContent);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var requestVoteResponse = JsonConvert.DeserializeObject<RequestVoteResponse>(content);
                    return requestVoteResponse;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(new EventId(1), exception, "Error in Send(RequestVote requestVote)");
                throw;
            }
        }

        public async Task<SendLeaderCommandResponse> Send(ICommand command, Guid leaderId)
        {
            try
            {
                var serverToSendMessageTo = _serviceRegistry.Get(RaftyServiceDiscoveryName.Get()).First(x => x.Id == leaderId);
                var json = JsonConvert.SerializeObject(command, Formatting.None, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
                var httpContent = new StringContent(json);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = serverToSendMessageTo.Location;
                    var response = await httpClient.PostAsync(_commandUrl, httpContent);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var requestVoteResponse = JsonConvert.DeserializeObject<SendLeaderCommandResponse>(content);
                    return requestVoteResponse;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(new EventId(1), exception, "Error in Send(ICommand command, Guid leaderId)");
                throw;
            }
        }

        public void Send(SendToSelf message)
        {
            if (_stopSendingMessages)
            {
                return;
            }

            try
            {
                _sleeping = true;
                Thread.Sleep(message.DelaySeconds * 1000);
                _sleeping = false;
                var typeOfMessage = message.Message.GetType();
                var handler = _sendToSelfHandlers[typeOfMessage];
                handler(message.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void SetServer(Server server)
        {
            _server = server;
        }

        public void Dispose()
        {
            while (_sleeping)
            {

            }
            _stopSendingMessages = true;
        }
    }
}