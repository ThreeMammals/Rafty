using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rafty.Commands;
using Rafty.Messages;
using Rafty.Messaging;
using Rafty.Responses;
using Rafty.ServiceDiscovery;
using Rafty.State;

namespace Rafty.Raft
{
    public class Server
    {
        private readonly IMessageBus _messageBus;
        private readonly List<ServerInCluster> _serversInCluster;
        private readonly IStateMachine _stateMachine;
        private readonly ILogger _logger;
        private bool _appendingEntries;
        private readonly object _lock = new object();
        private Guid _lastAppendEntriesMessageId;

        public Server(IMessageBus messageBus, List<ServerInCluster> remoteServers, IStateMachine stateMachine, ILogger logger)
        {
            _stateMachine = stateMachine;
            _logger = logger;
            _messageBus = messageBus;
            _serversInCluster = remoteServers ?? new List<ServerInCluster>();
            Id = Guid.NewGuid();
            Log = new List<Log>();
            NextIndex = new List<Next>();
            MatchIndex = new List<Match>();
            State = new Follower();
            SendElectionTimeoutMessage(1);
        }

        public int CurrentTerm { get; private set; }
        public Guid VotedFor { get; private set; }
        public List<Log> Log { get; private set; }
        public int CommitIndex { get; private set; }
        public int LastApplied { get; private set; }
        public List<Next> NextIndex { get; private set; }
        public List<Match> MatchIndex { get; private set; }
        public State State { get; private set; }
        public Guid Id { get; private set; }
        public int CurrentTermVotes { get; private set; }
        public int CurrentTermAppendEntriesResponse { get; private set; }
        public int CountOfRemoteServers => _serversInCluster.Count;
        public Guid LeaderId { get; private set; }

        public RequestVoteResponse Receive(RequestVote requestVote)
        {
            _logger.LogDebug($"Server: {Id} received request vote in term: {CurrentTerm}");

            if (!_serversInCluster.Select(x => x.Id).Contains(requestVote.CandidateId))
            {
                var remoteServer = new ServerInCluster(requestVote.CandidateId);
                _serversInCluster.Add(remoteServer);
            }

            // If RPC request or response contains term T > currentTerm:
            // set currentTerm = T, convert to follower (§5.1)
            if (requestVote.Term > CurrentTerm)
            {
                BecomeFollowerAndMatchTerm(requestVote.Term, requestVote.CandidateId);
                _logger.LogDebug($"Server: {Id} received request vote in term: {CurrentTerm}, became follower");
            }
            /*
            1.Reply false if term < currentTerm(�5.1)*/
            if (requestVote.Term < CurrentTerm)
            {
                _logger.LogDebug($"Server: {Id} received request vote in term: {CurrentTerm}, voted false because term was less than current term");
                return new RequestVoteResponse(CurrentTerm, false, requestVote.CandidateId, Id);
            }

            lock (_lock)
            {
                /*
                2.If votedFor is null or candidateId, and candidate�s log is at
                least as up - to - date as receiver�s log, grant vote(�5.2, �5.4)*/
                if (VotedForIsNullOrAlreadyVotingForCandidate(requestVote))
                {
                    if (CandidatesLogIsAtLeastUpToDateAsServers(requestVote))
                    {
                        VotedFor = requestVote.CandidateId;
                        _logger.LogDebug($"Server: {Id} received request vote in term: {CurrentTerm}, voted true");
                        return new RequestVoteResponse(CurrentTerm, true, requestVote.CandidateId, Id);
                    }
                }
            }

            _logger.LogDebug($"Server: {Id} received request vote in term: {CurrentTerm}, voted false because already voted for or candidates log isnt up to date");
            return new RequestVoteResponse(CurrentTerm, false, requestVote.CandidateId, Id);
        }

        public void Receive(BecomeCandidate becomeCandidate)
        {
            if ((State is Candidate || State is Follower) && becomeCandidate.LastAppendEntriesMessageIdFromLeader == _lastAppendEntriesMessageId)
            {
                State = new Candidate();
                CurrentTerm++;
                VotedFor = default(Guid);
                CurrentTermVotes = 0;
                var requestVoteResponse = new RequestVoteResponse(CurrentTerm, true, Id, Id);
                Receive(requestVoteResponse);
                var remoteServers = GetRemoteServers();
                var tasks = new Task<RequestVoteResponse>[remoteServers.Count];
                for (int i = 0; i < tasks.Length; i++)
                {
                    var lastLogIndex = Log.Count > 0 ? Log.Count - 1 : 0;
                    var lastLogTerm = Log.Count > 0 ? Log[lastLogIndex].Term : 0;
                    var requestVote = new RequestVote(CurrentTerm, Id, lastLogIndex, lastLogTerm, remoteServers[i].Id);
                    tasks[i] = _messageBus.Send(requestVote);
                }

                Task.WaitAll(tasks);

                foreach (var task in tasks)
                {
                    Receive(task.Result);
                }
            }

            SendElectionTimeoutMessage(10);
        }

        public AppendEntriesResponse Receive(AppendEntries appendEntries)
        {

            if (!_serversInCluster.Select(x => x.Id).Contains(appendEntries.LeaderId))
            {
                var remoteServer = new ServerInCluster(appendEntries.LeaderId);
                _serversInCluster.Add(remoteServer);
            }

            if (State is Leader)
            {
                BecomeFollowerAndMatchTerm(appendEntries.Term, appendEntries.LeaderId);
            }

            _lastAppendEntriesMessageId = appendEntries.MessageId;

            // If RPC request or response contains term T > currentTerm:
            // set currentTerm = T, convert to follower (§5.1)
            if (appendEntries.Term > CurrentTerm || State is Candidate)
            {
                BecomeFollowerAndMatchTerm(appendEntries.Term, appendEntries.LeaderId);
            }

            /*
             1.Reply false if term < currentTerm(�5.1)
             */
            if (appendEntries.Term < CurrentTerm)
            {
                return new AppendEntriesResponse(CurrentTerm, false, Id, appendEntries.LeaderId);
            }

            if (appendEntries.Entry == null)
            {
                //todo heartbeat reset election timer
                return new AppendEntriesResponse(CurrentTerm, true, Id, appendEntries.LeaderId);
            }

            /*
            2.Reply false if log doesn�t contain an entry at prevLogIndex
            whose term matches prevLogTerm(�5.3)*/
            if (Log.Count > 0 && Log.Count > appendEntries.PreviousLogIndex && Log[appendEntries.PreviousLogIndex].Term != appendEntries.PreviousLogTerm)
            {
                return new AppendEntriesResponse(CurrentTerm, false, Id, appendEntries.LeaderId);
            }

            /* 3.If an existing entry conflicts with a new one(same index
             but different terms), delete the existing entry and all that
             follow it*/
            var newEntry = appendEntries.Entry;

            if(Log.Count > 0 && Log.Count > appendEntries.PreviousLogIndex)
            {
                var existingEntry = Log[appendEntries.PreviousLogIndex];
                if(existingEntry.Term != newEntry.Term)
                {
                    Log.Remove(existingEntry);
                }
            }
            else if(Log.Count > 0 && Log.Count > appendEntries.PreviousLogIndex + 1)
            {
                var existingEntry = Log[appendEntries.PreviousLogIndex + 1];
                if(existingEntry.Term != newEntry.Term)
                {
                    Log.Remove(existingEntry);
                }
            }

            /*   4.Append any new entries not already in the log
           */
            Log.Add(newEntry);
            CommitIndex = CommitIndex + 1;

            /*
            5.If leaderCommit > commitIndex, set commitIndex =
            min(leaderCommit, index of last new entry)
            */
            if (appendEntries.LeaderCommit > CommitIndex)
            {
                CommitIndex = Math.Min(appendEntries.LeaderCommit, Log.Count - 1);
            }

            /* If commitIndex > lastApplied: increment lastApplied, apply
            log[lastApplied] to state machine(§5.3)*/
            if (CommitIndex > LastApplied)
            {
                if(Log.Count > 1)
                {
                    LastApplied++;
                }
                _stateMachine.Apply(Log[LastApplied].Command);
                return new AppendEntriesResponse(CurrentTerm, true, Id, appendEntries.LeaderId);

            }

            return new AppendEntriesResponse(CurrentTerm, false, Id, appendEntries.LeaderId);
        }

        public void Receive(SendHeartbeat sendHeartbeat)
        {
            if (State is Leader)
            {
                var remoteServers = GetRemoteServers();

                var tasks = new Task<AppendEntriesResponse>[remoteServers.Count];
                for (int i = 0; i < tasks.Length; i++)
                {
                    var lastLogIndex = Log.Count > 0 ? Log.Count - 1 : 0;
                    var lastLogTerm = lastLogIndex > 0 ? Log[lastLogIndex].Term : 0;
                    var appendEntries = new AppendEntries(CurrentTerm, Id, lastLogIndex, lastLogTerm, null, CommitIndex, remoteServers[i].Id);
                    tasks[i] = _messageBus.Send(appendEntries);
                }

                Task.WaitAll(tasks);

                foreach (var task in tasks)
                {
                    if (task.Result.Term > CurrentTerm)
                    {
                        BecomeFollowerAndMatchTerm(task.Result.Term, task.Result.FollowerId);
                    }
                }

                _messageBus.Publish(new SendToSelf(new SendHeartbeat()));
            }
        }

        public SendLeaderCommandResponse Receive(ICommand command)
        {
            if (State is Follower)
            {
                _messageBus.Send(command, LeaderId);
            }

            if (State is Leader)
            {
                _logger.LogDebug("Server Received Command");
                _appendingEntries = true;
                Log.Add(new Log(CurrentTerm, (FakeCommand)command));
                CommitIndex = Log.Count - 1;

                var remoteServers = GetRemoteServers();

                var tasks = new Task<AppendEntriesResponse>[remoteServers.Count];

                for (int i = 0; i < tasks.Length; i++)
                {
                    var next = NextIndex.FirstOrDefault(x => x.Id == remoteServers[i].Id);
                    if (next == null)
                    {
                        var nextLogIndex = 0;
                        next = new Next(remoteServers[i].Id, nextLogIndex);
                        NextIndex.Add(next);
                    }
                    var match = MatchIndex.FirstOrDefault(x => x.Id == remoteServers[i].Id);
                    if (match == null)
                    {
                        match = new Match(remoteServers[i].Id, 0);
                        MatchIndex.Add(match);
                    }
                    var lastLogIndex = Log.Count > 0 ? Log.Count - 1 : 0;
                    var lastLogTerm = lastLogIndex > 0 ? Log[match.MatchIndex].Term : 0;

                    // If last log index ≥ nextIndex for a follower: send
                    // AppendEntries RPC with log entries starting at nextIndex
                    if (lastLogIndex >= next.NextIndex)
                    {
                        var log = Log[next.NextIndex];
                        var appendEntries = new AppendEntries(CurrentTerm, Id, match.MatchIndex, lastLogTerm, log, CommitIndex, remoteServers[i].Id);
                        tasks[i] = _messageBus.Send(appendEntries);
                    }
                }

                Task.WaitAll(tasks);
                int counter = 0;
                foreach (var task in tasks)
                {
                    _logger.LogDebug($"Processing Append entries counter: {counter}");
                    _logger.LogDebug($"Processing Append entries result was: {task.Result.Success} counter: {counter}");
                    Receive(task.Result);
                    _logger.LogDebug($"Processed Append entries counter: {counter}");
                }
            }

            return new SendLeaderCommandResponse();
        }

        private void BecomeLeader()
        {
            if (State is Candidate)
            {
                State = new Leader();
                LeaderId = Id;
                _messageBus.Publish(new SendToSelf(new SendHeartbeat()));
                NextIndex = new List<Next>();
                MatchIndex = new List<Match>();

                var remoteServers = GetRemoteServers();

                for (var i = 0; i < remoteServers.Count; i++)
                {
                    var nextLogIndex = Log.Count;
                    var next = new Next(remoteServers[i].Id, nextLogIndex);
                    NextIndex.Add(next);
                    var match = new Match(remoteServers[i].Id, 0);
                    MatchIndex.Add(match);
                }

                var tasks = new Task<AppendEntriesResponse>[remoteServers.Count];
                for (var i = 0; i < tasks.Length; i++)
                {
                    var lastLogIndex = Log.Count > 0 ? Log.Count - 1 : 0;
                    var lastLogTerm = lastLogIndex > 0 ? Log[lastLogIndex].Term : 0;
                    var appendEntries = new AppendEntries(CurrentTerm, Id, lastLogIndex, lastLogTerm, null, CommitIndex, remoteServers[i].Id);
                    tasks[i] = _messageBus.Send(appendEntries);
                }

                Task.WaitAll(tasks);

                foreach (var task in tasks)
                {
                    if (task.Result.Term > CurrentTerm)
                    {
                        BecomeFollowerAndMatchTerm(task.Result.Term, task.Result.FollowerId);
                    }
                }
            }
        }

        private List<ServerInCluster> GetRemoteServers()
        {
            return _serversInCluster.Where(x => x.Id != Id).ToList();
        }

        private void BecomeFollowerAndMatchTerm(int term, Guid leaderId)
        {
            LeaderId = leaderId;
            CurrentTerm = term;
            VotedFor = default(Guid);
            State = new Follower();
            SendElectionTimeoutMessage(10);
        }

        private void Receive(RequestVoteResponse requestVoteResponse)
        {
            // If RPC request or response contains term T > currentTerm:
            // set currentTerm = T, convert to follower (§5.1)
            if (requestVoteResponse.Term > CurrentTerm)
            {
                BecomeFollowerAndMatchTerm(requestVoteResponse.Term, requestVoteResponse.VoterId);
                return;
            }

            if (State is Candidate && requestVoteResponse.VoteGranted)
            {
                CurrentTermVotes++;

                if (CurrentTermVotes >= (_serversInCluster.Count / 2) + 1)
                {
                    BecomeLeader();
                }
            }
        }

        private void Receive(AppendEntriesResponse appendEntriesResponse)
        {
            if (State is Leader)
            {
                // If RPC request or response contains term T > currentTerm:
                // set currentTerm = T, convert to follower (§5.1)
                if (appendEntriesResponse.Term > CurrentTerm)
                {
                    BecomeFollowerAndMatchTerm(appendEntriesResponse.Term, appendEntriesResponse.FollowerId);
                }

                // If successful: update nextIndex and matchIndex for
                // follower (§5.3)
                if (State is Leader && appendEntriesResponse.Success)
                {
                    var currentNext = NextIndex.First(x => x.Id == appendEntriesResponse.FollowerId);
                    NextIndex.Remove(currentNext);
                    var nextLogIndex = Log.Count;
                    var next = new Next(appendEntriesResponse.FollowerId, nextLogIndex);
                    NextIndex.Add(next);

                    var currentMatch = MatchIndex.First(x => x.Id == appendEntriesResponse.FollowerId);
                    MatchIndex.Remove(currentMatch);
                    var nextMatchIndex = currentMatch.MatchIndex + 1;
                    var match = new Match(appendEntriesResponse.FollowerId, currentNext.NextIndex);
                    MatchIndex.Add(match);
                }

                if (State is Leader && _appendingEntries && appendEntriesResponse.Success)
                {
                    CurrentTermAppendEntriesResponse++;

                    if (CurrentTermAppendEntriesResponse >= (_serversInCluster.Count / 2) + 1)
                    {
                        if ((CommitIndex == 0 && LastApplied == 0) || CommitIndex > LastApplied)
                        {
                            var entry = Log[Log.Count - 1];
                            LastApplied = Log.Count - 1;
                            _stateMachine.Apply(entry.Command);
                        }

                        CurrentTermAppendEntriesResponse = 0;
                        _appendingEntries = false;
                    }
                }
                // If AppendEntries fails because of log inconsistency:
                // decrement nextIndex and retry (§5.3)
                else if (State is Leader && _appendingEntries)
                {
                    var lastLogIndex = Log.Count - 1;
                    var lastLogTerm = Log[lastLogIndex].Term;
                    var next = NextIndex.First(x => x.Id == appendEntriesResponse.FollowerId);

                    if (lastLogIndex >= next.NextIndex)
                    {
                        var log = Log[next.NextIndex];
                        var appendEntries = new AppendEntries(CurrentTerm, Id, lastLogIndex, lastLogTerm, log, CommitIndex, appendEntriesResponse.FollowerId);
                        var task = _messageBus.Send(appendEntries);

                        Task.WaitAll(task);

                        Receive(task.Result);
                    }
                }
            }
        }

        private bool CandidatesLogIsAtLeastUpToDateAsServers(RequestVote requestVote)
        {
            return requestVote.LastLogIndex > Log.Count - 1 || requestVote.LastLogIndex == Log.Count - 1
                   && requestVote.LastLogTerm == Log[Log.Count - 1].Term;
        }

        private bool VotedForIsNullOrAlreadyVotingForCandidate(RequestVote requestVote)
        {
            return VotedFor == default(Guid) || VotedFor == requestVote.CandidateId;
        }

        private void SendElectionTimeoutMessage(int delayInSeconds)
        {
            var becomeCandidate = new BecomeCandidate(_lastAppendEntriesMessageId);
            var sendToSelf = new SendToSelf(becomeCandidate, delayInSeconds);
            _messageBus.Publish(sendToSelf);
        }
    }
}