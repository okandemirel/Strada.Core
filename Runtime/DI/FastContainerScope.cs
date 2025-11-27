using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Strada.Core.DI
{
    public sealed class FastContainerScope : IContainerScope, IIndexResolver
    {
        private readonly FastContainer _parent;
        private readonly Func<IIndexResolver, object>[] _factories;
        private readonly Func<IIndexResolver, object>[] _scopedFactories;
        private readonly Lifetime[] _lifetimes;
        private readonly int[] _typeIdToIndex;
        private readonly int _maxTypeId;
        private readonly object[] _parentSingletons;
        private readonly object[] _scopedInstances;
        private bool _disposed;

        public IContainer Parent => _parent;

        internal FastContainerScope(
            FastContainer parent,
            Func<IIndexResolver, object>[] factories,
            Func<IIndexResolver, object>[] scopedFactories,
            Lifetime[] lifetimes,
            int[] typeIdToIndex,
            int maxTypeId,
            object[] parentSingletons)
        {
            _parent = parent;
            _factories = factories;
            _scopedFactories = scopedFactories;
            _lifetimes = lifetimes;
            _typeIdToIndex = typeIdToIndex;
            _maxTypeId = maxTypeId;
            _parentSingletons = parentSingletons;
            _scopedInstances = new object[factories.Length];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Resolve<T>() where T : class
        {
            int typeId = TypeRegistry.GetId<T>();
            return (T)ResolveById(typeId);
        }

        public object Resolve(Type type)
        {
            int typeId = TypeRegistry.GetId(type);
            return ResolveById(typeId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object ResolveById(int typeId)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastContainerScope));

            if (typeId > _maxTypeId)
                throw new InvalidOperationException($"Type with ID '{typeId}' is not registered");

            int index = _typeIdToIndex[typeId];
            if (index < 0)
                throw new InvalidOperationException($"Type with ID '{typeId}' is not registered");

            return ResolveByIndex(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object ResolveByIndex(int index)
        {
            var lifetime = _lifetimes[index];

            if (lifetime == Lifetime.Singleton)
            {
                var existing = Volatile.Read(ref _parentSingletons[index]);
                if (existing != null)
                    return existing;

                return _parent.ResolveByIndex(index);
            }

            if (lifetime == Lifetime.Scoped)
            {
                var existing = Volatile.Read(ref _scopedInstances[index]);
                if (existing != null)
                    return existing;

                var instance = _scopedFactories[index](this);

                var prev = Interlocked.CompareExchange(ref _scopedInstances[index], instance, null);
                if (prev != null)
                {
                    (instance as IDisposable)?.Dispose();
                    return prev;
                }

                return instance;
            }

            return _factories[index](this);
        }

        public bool TryResolve<T>(out T instance) where T : class
        {
            if (_disposed)
            {
                instance = null;
                return false;
            }

            int typeId = TypeRegistry.GetId<T>();

            if (typeId > _maxTypeId || _typeIdToIndex[typeId] < 0)
            {
                instance = null;
                return false;
            }

            instance = (T)ResolveById(typeId);
            return true;
        }

        public IContainerScope CreateScope()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastContainerScope));

            return new FastContainerScope(_parent, _factories, _scopedFactories, _lifetimes, _typeIdToIndex, _maxTypeId, _parentSingletons);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRegistered<T>() where T : class
        {
            int typeId = TypeRegistry.GetId<T>();
            return typeId <= _maxTypeId && _typeIdToIndex[typeId] >= 0;
        }

        public bool IsRegistered(Type type)
        {
            int typeId = TypeRegistry.GetId(type);
            return typeId <= _maxTypeId && _typeIdToIndex[typeId] >= 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            for (int i = 0; i < _scopedInstances.Length; i++)
            {
                var instance = Volatile.Read(ref _scopedInstances[i]);
                (instance as IDisposable)?.Dispose();
                _scopedInstances[i] = null;
            }
        }
    }
}