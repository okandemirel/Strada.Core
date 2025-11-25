using System;
using System.Runtime.CompilerServices;

namespace Strada.Core.Pooling
{
    public abstract class PooledObject<T> : IPoolable<T>, IDisposable where T : PooledObject<T>
    {
        private ObjectPool<T> _pool;

        public bool IsPooled => _pool != null;

        void IPoolable<T>.SetPool(ObjectPool<T> pool) => _pool = pool;

        public virtual void OnSpawn() { }

        public virtual void OnDespawn() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReturnToPool()
        {
            _pool?.Despawn((T)this);
        }

        public void Dispose()
        {
            ReturnToPool();
        }
    }

    public readonly struct PooledHandle<T> : IDisposable where T : class
    {
        private readonly T _instance;
        private readonly ObjectPool<T> _pool;

        public T Value => _instance;

        internal PooledHandle(T instance, ObjectPool<T> pool)
        {
            _instance = instance;
            _pool = pool;
        }

        public void Dispose()
        {
            _pool?.Despawn(_instance);
        }

        public static implicit operator T(PooledHandle<T> handle) => handle._instance;
    }

    public static class ObjectPoolExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledHandle<T> SpawnScoped<T>(this ObjectPool<T> pool) where T : class
        {
            return new PooledHandle<T>(pool.Spawn(), pool);
        }
    }
}
