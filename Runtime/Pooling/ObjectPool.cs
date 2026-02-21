using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.Pooling
{
    public sealed class ObjectPool<T> : IDisposable where T : class
    {
        private readonly Stack<T> _available;
        private readonly Func<T> _factory;
        private readonly Action<T> _onSpawn;
        private readonly Action<T> _onDespawn;
        private readonly int _maxSize;
        private int _totalCreated;
        private bool _disposed;

        public int AvailableCount => _available.Count;
        public int TotalCreated => _totalCreated;
        public int ActiveCount => _totalCreated - _available.Count;

        public ObjectPool(Func<T> factory, int initialSize = 0, int maxSize = int.MaxValue)
            : this(factory, null, null, initialSize, maxSize) { }

        public ObjectPool(Func<T> factory, Action<T> onSpawn, Action<T> onDespawn, int initialSize = 0, int maxSize = int.MaxValue)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onSpawn = onSpawn;
            _onDespawn = onDespawn;
            _maxSize = maxSize;
            _available = new Stack<T>(Math.Max(initialSize, 16));

            Prewarm(initialSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Spawn()
        {
            T instance;

            if (_available.Count > 0)
            {
                instance = _available.Pop();
            }
            else
            {
                instance = _factory();
                _totalCreated++;

                if (instance is IPoolable<T> poolable)
                    poolable.SetPool(this);
            }

            _onSpawn?.Invoke(instance);

            if (instance is IPoolable p)
                p.OnSpawn();

            return instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Despawn(T instance)
        {
            if (instance == null) return;
            if (_disposed) return;

            if (instance is IPoolable p)
                p.OnDespawn();

            _onDespawn?.Invoke(instance);

            if (_available.Count < _maxSize)
                _available.Push(instance);
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = _factory();
                _totalCreated++;

                if (instance is IPoolable<T> poolable)
                    poolable.SetPool(this);

                _available.Push(instance);
            }
        }

        public void Clear()
        {
            int cleared = _available.Count;
            while (_available.Count > 0)
            {
                var instance = _available.Pop();
                if (instance is IDisposable d)
                    d.Dispose();
            }
            _totalCreated -= cleared;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }
    }
}
