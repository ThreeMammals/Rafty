namespace Rafty.Concensus
{
    public sealed class RequestVoteResponse
    {
        public RequestVoteResponse(bool grant)
        {
            Grant = grant;
        }

        public bool Grant { get; private set; }
    }
}