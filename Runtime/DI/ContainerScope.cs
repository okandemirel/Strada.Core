using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Strada.Core.DI
{
    public sealed class ContainerScope : IContainerScope, IIndexResolver
    {
        private readonly Container _parent;
        private readonly Func<IIndexResolver, object>[] _factories;
        private readonly Func<IIndexResolver, object>[] _scopedFactories;
        private readonly Lifetime[] _lifetimes;
        private readonly int[] _typeIdToIndex;
        private readonly int _maxTypeId;
        private readonly object[] _parentSingletons;
        private readonly object[] _scopedInstances;
        private volatile bool _disposed;
        private readonly object _disposeLock = new object();

        public IContainer Parent => _parent;

        internal ContainerScope(
            Container parent,
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
            return (T)ResolveById(typeId, typeof(T));
        }

        public object Resolve(Type type)
        {
            int typeId = TypeRegistry.GetId(type);
            return ResolveById(typeId, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object ResolveById(int typeId, Type requestedType)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ContainerScope));

            if (typeId > _maxTypeId)
                throw new InvalidOperationException($"Type '{requestedType.FullName}' is not registered in the container");

            int index = _typeIdToIndex[typeId];
            if (index < 0)
                throw new InvalidOperationException($"Type '{requestedType.FullName}' is not registered in the container");

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

            instance = (T)ResolveByIndex(_typeIdToIndex[typeId]);
            return true;
        }

        public IContainerScope CreateScope()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ContainerScope));

            return new ContainerScope(_parent, _factories, _scopedFactories, _lifetimes, _typeIdToIndex, _maxTypeId, _parentSingletons);
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

            lock (_disposeLock)
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
}
