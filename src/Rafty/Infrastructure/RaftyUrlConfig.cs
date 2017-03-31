namespace Rafty.Infrastructure
{
    public static class RaftyUrlConfig
    {
        public static (string appendEntriesUrl, string requestVoteUrl, string commandUrl) Get(string raftyBasePath)
        {
            if (raftyBasePath == null)
            {
                raftyBasePath = string.Empty;
            }

            if (raftyBasePath.Length > 0 && raftyBasePath[0] != '/')
            {
                raftyBasePath = $"/{raftyBasePath}";
            }

            var appendEntriesUrl = $"{raftyBasePath}/appendentries";
            var requestVoteUrl = $"{raftyBasePath}/requestvote";
            var commandUrl = $"{raftyBasePath}/command";

            return (appendEntriesUrl, requestVoteUrl, commandUrl);
        }
    }

    public static class RaftyServiceDiscoveryName
    {
        public static string Get()
        {
            return "rafty";
        }
    }
}