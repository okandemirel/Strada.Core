using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.Pooling
{
    public sealed class PoolRegistry : IDisposable
    {
        private readonly Dictionary<Type, object> _pools = new(32);
        private bool _disposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ObjectPool<T> GetOrCreate<T>(Func<T> factory, int initialSize = 0, int maxSize = int.MaxValue) where T : class
        {
            var type = typeof(T);

            if (_pools.TryGetValue(type, out var existing))
                return (ObjectPool<T>)existing;

            var pool = new ObjectPool<T>(factory, initialSize, maxSize);
            _pools[type] = pool;
            return pool;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ObjectPool<T> Get<T>() where T : class
        {
            return _pools.TryGetValue(typeof(T), out var pool) ? (ObjectPool<T>)pool : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(out ObjectPool<T> pool) where T : class
        {
            if (_pools.TryGetValue(typeof(T), out var obj))
            {
                pool = (ObjectPool<T>)obj;
                return true;
            }
            pool = null;
            return false;
        }

        public void Register<T>(ObjectPool<T> pool) where T : class
        {
            _pools[typeof(T)] = pool;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Spawn<T>() where T : class
        {
            var pool = Get<T>();
            return pool?.Spawn();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Despawn<T>(T instance) where T : class
        {
            var pool = Get<T>();
            pool?.Despawn(instance);
        }

        public void Clear()
        {
            foreach (var pool in _pools.Values)
            {
                if (pool is IDisposable d)
                    d.Dispose();
            }
            _pools.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }
    }
}
