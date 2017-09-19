namespace Rafty.Concensus
{
    public interface ISettings
    {
        /// <summary>
        // The minimum follower timeout in milliseconds.
        /// </summary>
        int MinTimeout { get; }

        /// <summary>
        // The maximum follower timeout in milliseconds.
        /// </summary>
        int MaxTimeout { get; }

        /// <summary>
        // The leader heartbeat timeout in milliseconds.
        /// </summary>
        int HeartbeatTimeout { get; }

        /// <summary>
        // The command timeout in milliseconds.
        /// </summary>
        int CommandTimeout {get;}
    }
}