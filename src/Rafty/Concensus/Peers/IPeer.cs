namespace Rafty.Concensus.Peers
{
    using System.Threading.Tasks;
    using FiniteStateMachine;
    using Infrastructure;
    using Messages;

    public interface IPeer
    {
        /// <summary>
        /// This will return the peers ID.
        /// </summary>
        string Id {get;}

        /// <summary>
        /// This will make a requestvote request to the given peer. You must implement the transport.
        /// </summary>
        Task<RequestVoteResponse> Request(RequestVote requestVote);

        /// <summary>
        /// This will make a appendentries request to the given peer. You must implement the transport.
        /// </summary>
        Task<AppendEntriesResponse> Request(AppendEntries appendEntries);

        /// <summary>
        /// This will make a command request ot the given peer. You must implement the transport.
        /// </summary>
        Task<Response<T>> Request<T>(T command) where T : ICommand;
    }
}