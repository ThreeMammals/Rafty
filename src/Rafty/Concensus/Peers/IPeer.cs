using System;
using Rafty.FiniteStateMachine;

namespace Rafty.Concensus
{
    public interface IPeer
    {
        /// <summary>
        /// This will return the peers ID.
        /// </summary>
        Guid Id {get;}

        /// <summary>
        /// This will make a requestvote request to the given peer. You must implement the transport.
        /// </summary>
        RequestVoteResponse Request(RequestVote requestVote);

        /// <summary>
        /// This will make a appendentries request to the given peer. You must implement the transport.
        /// </summary>
        AppendEntriesResponse Request(AppendEntries appendEntries);

        /// <summary>
        /// This will make a command request ot the given peer. You must implement the transport.
        /// </summary>
        Response<T> Request<T>(T command) where T : ICommand;
    }
}