namespace Rafty.Infrastructure
{
    public interface IReportable
    {
        string Name { get; }
        int Count { get; }
    }
}