namespace Rafty.Concensus.Node
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FiniteStateMachine;
    using Infrastructure;
    using Log;
    using Messages;
    using Microsoft.Extensions.Logging;
    using Peers;
    using States;

    public class Node : INode
    { 
        private readonly IFiniteStateMachine _fsm;
        private readonly ILog _log;
        private readonly Func<CurrentState, List<IPeer>> _getPeers;
        private readonly IRandomDelay _random;
        private readonly ISettings _settings;
        private IRules _rules;
        private IPeersProvider _peersProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<Node> _logger;
        private readonly SemaphoreSlim _appendingEntries = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _requestVote = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _acceptCommand = new SemaphoreSlim(1, 1);

        public Node(
            IFiniteStateMachine fsm,
            ILog log,
            ISettings settings,
            IPeersProvider peersProvider,
            ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<Node>();
            _fsm = fsm;
            _log = log;
            _random = new RandomDelay();
            _settings = settings;
            _peersProvider = peersProvider;
            _getPeers = state => {
                var peers = _peersProvider.Get();
                var peersThatAreNotThisServer = peers.Where(p => p?.Id != state.Id).ToList();
                return peersThatAreNotThisServer;
            };
        }

        public IState State { get; private set; }

        public void Start(NodeId id)
        {
            _rules = new Rules(_loggerFactory, id);

            BecomeFollower(State?.CurrentState ?? new CurrentState(id.Id, 0, default(string), 0, 0, default(string)));
        }

        public void BecomeCandidate(CurrentState state)
        {
            State?.Stop();
            _logger.LogInformation($"{state.Id} became candidate");
            var candidate = new Candidate(state, _fsm, _getPeers(state), _log, _random, this, _settings, _rules, _loggerFactory);
            State = candidate;
            candidate.BeginElection();
        }

        public void BecomeLeader(CurrentState state)
        {
            State.Stop();
            _logger.LogInformation($"{state.Id} became leader");
            State = new Leader(state, _fsm, _getPeers, _log, this, _settings, _rules, _loggerFactory);
        }

        public void BecomeFollower(CurrentState state)
        {
            State?.Stop();
            _logger.LogInformation($"{state.Id} became follower");
            State = new Follower(state, _fsm, _log, _random, this, _settings, _rules, _getPeers(state), _loggerFactory);
        }

        public async Task<AppendEntriesResponse> Handle(AppendEntries appendEntries)
        {
            try
            {
                await _appendingEntries.WaitAsync();

                var response = await State.Handle(appendEntries);

                if (appendEntries.Entries.Any())
                {
                    _logger.LogInformation($"{State.GetType().Name} id: {State.CurrentState.Id} responded to appendentries with success: {response.Success} and term: {response.Term}");
                }

                return response;
            }
            finally
            {
                _appendingEntries.Release();
            }
        }

        public async Task<RequestVoteResponse> Handle(RequestVote requestVote)
        {
            try
            {
                await _requestVote.WaitAsync();

                return await State.Handle(requestVote);
            }
            finally
            {
                _requestVote.Release();
            }
        }

        public async Task<Response<T>> Accept<T>(T command) where T : ICommand
        {
            try
            {
                await _acceptCommand.WaitAsync();

                return await State.Accept(command);
            }
            finally
            {
                _acceptCommand.Release();

            }
        }

        public void Stop()
        {
            State.Stop();
            State = null;
        }

        public void Pause()
        {
            State.Stop();
        }
    }
}