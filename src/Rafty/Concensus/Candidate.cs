namespace Rafty.Concensus
{
    public sealed class Candidate : IState
    {
        private readonly ISendToSelf _sendToSelf;
        private int _votesThisElection;

        public Candidate(CurrentState currentState, ISendToSelf sendToSelf)
        {
            _sendToSelf = sendToSelf;
            // • On conversion to candidate, start election:
            // • Increment currentTerm
            var nextTerm = currentState.CurrentTerm + 1;
            // • Vote for self
            _votesThisElection++;
            var votedFor = currentState.Id;
            var nextState = new CurrentState(currentState.Id, currentState.Peers, nextTerm, votedFor,
                currentState.Timeout);
            CurrentState = nextState;
        }

        public CurrentState CurrentState { get; }

        public IState Handle(Timeout timeout)
        {
            return this;
        }

        public IState Handle(BeginElection beginElection)
        {
            // • On conversion to candidate, start election:
            // • Reset election timer
            _sendToSelf.Publish(new Timeout(CurrentState.Timeout));
            // • Send RequestVote RPCs to all other servers
            CurrentState.Peers.ForEach(peer =>
            {
                var requestVoteResponse = peer.Request(new RequestVote());

                if (requestVoteResponse.Grant)
                {
                    _votesThisElection++;
                }
            });

            //If votes received from majority of servers: become leader
            if (_votesThisElection >= (CurrentState.Peers.Count + 1) / 2 + 1)
            {
                return new Leader(CurrentState);
            }

            return new Follower(CurrentState, _sendToSelf);
        }
    }
}