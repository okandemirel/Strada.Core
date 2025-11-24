using System;
using System.Collections.Generic;

namespace Strada.Core.DI
{
    public sealed class FastContainerScope : IContainerScope
    {
        private readonly IContainer _parent;
        private readonly Dictionary<int, Func<IContainer, object>> _factories;
        private readonly Dictionary<int, object> _scoped;
        private readonly Dictionary<Type, int> _typeToHash;
        private readonly HashSet<int> _scopedHashes;
        private readonly object _lock = new object();
        private bool _disposed;

        public IContainer Parent => _parent;

        internal FastContainerScope(
            IContainer parent,
            Dictionary<int, Func<IContainer, object>> factories,
            Dictionary<Type, int> typeToHash,
            HashSet<int> scopedHashes)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _factories = factories ?? throw new ArgumentNullException(nameof(factories));
            _typeToHash = typeToHash ?? throw new ArgumentNullException(nameof(typeToHash));
            _scopedHashes = scopedHashes ?? throw new ArgumentNullException(nameof(scopedHashes));
            _scoped = new Dictionary<int, object>();
        }

        public T Resolve<T>() where T : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastContainerScope));

            var hash = GetTypeHash(typeof(T));

            if (_scoped.TryGetValue(hash, out var instance))
                return (T)instance;

            if (_scopedHashes.Contains(hash))
            {
                lock (_lock)
                {
                    if (_scoped.TryGetValue(hash, out instance))
                        return (T)instance;

                    if (_factories.TryGetValue(hash, out var factory))
                    {
                        instance = factory(this);
                        _scoped[hash] = instance;
                        return (T)instance;
                    }
                }
            }

            return _parent.Resolve<T>();
        }

        public object Resolve(Type type)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastContainerScope));

            var hash = GetTypeHash(type);

            if (_scoped.TryGetValue(hash, out var instance))
                return instance;

            if (_scopedHashes.Contains(hash))
            {
                lock (_lock)
                {
                    if (_scoped.TryGetValue(hash, out instance))
                        return instance;

                    if (_factories.TryGetValue(hash, out var factory))
                    {
                        instance = factory(this);
                        _scoped[hash] = instance;
                        return instance;
                    }
                }
            }

            return _parent.Resolve(type);
        }

        public bool TryResolve<T>(out T instance) where T : class
        {
            try
            {
                instance = Resolve<T>();
                return true;
            }
            catch
            {
                instance = null;
                return false;
            }
        }

        public IContainerScope CreateScope()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastContainerScope));

            return new FastContainerScope(_parent, _factories, _typeToHash, _scopedHashes);
        }

        public bool IsRegistered<T>() where T : class
        {
            return _typeToHash.ContainsKey(typeof(T));
        }

        public bool IsRegistered(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _typeToHash.ContainsKey(type);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var instance in _scoped.Values)
            {
                if (instance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            _scoped.Clear();
        }

        private int GetTypeHash(Type type)
        {
            if (_typeToHash.TryGetValue(type, out var hash))
                return hash;

            var typeName = type.FullName ?? type.Name;
            return FNV1aHash(typeName);
        }

        private static int FNV1aHash(string text)
        {
            unchecked
            {
                const int fnvPrime = 16777619;
                int hash = (int)2166136261;

                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= fnvPrime;
                }

                return hash;
            }
        }
    }
}
