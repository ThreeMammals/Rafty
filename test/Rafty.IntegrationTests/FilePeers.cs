using System.Collections.Generic;

namespace Rafty.IntegrationTests
{
    public class FilePeers
    {
        public FilePeers()
        {
            Peers = new List<FilePeer>();
        }

        public List<FilePeer> Peers {get; set;}
    }
}
