namespace Strada.Core.Sync
{
    public interface IViewPool
    {
        int AvailableCount { get; }
        int ActiveCount { get; }
        int TotalCreated { get; }
        void Prewarm(int count);
        void DespawnAll();
        void Clear();
    }
}
