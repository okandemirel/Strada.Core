namespace Strada.Core.Sync
{
    /// <summary>
    /// Non-generic interface for view pools to enable type-erased storage in PoolManager.
    /// </summary>
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
