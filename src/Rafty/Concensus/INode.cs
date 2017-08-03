using System;
using System.Collections.Generic;

namespace Rafty.Concensus
{
    public interface INode
    {
        Guid Id {get;}
        IState State { get; }
        void Dispose();
        AppendEntriesResponse Handle(AppendEntries appendEntries);
        void Handle(Message message);
        Response<T> Accept<T>(T command);
        void Start(List<IPeer> peers, TimeSpan timeout);
    }
}