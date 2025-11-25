namespace Strada.Core.Pooling
{
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    public interface IPoolable<T> : IPoolable where T : class
    {
        void SetPool(ObjectPool<T> pool);
    }
}
