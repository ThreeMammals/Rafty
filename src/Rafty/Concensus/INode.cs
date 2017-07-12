namespace Rafty.Concensus
{
    public interface INode
    {
        IState State { get; }
        void Dispose();
        AppendEntriesResponse Handle(AppendEntries appendEntries);
        void Handle(Message message);
    }
}