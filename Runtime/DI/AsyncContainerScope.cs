using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Strada.Core.DI
{
    public sealed class AsyncContainerScope : IContainerScope, IAsyncDisposable
    {
        private readonly IContainerScope _innerScope;
        private readonly Func<Type, CancellationToken, ValueTask<object>>[] _asyncFactories;
        private readonly int[] _typeIdToAsyncIndex;
        private readonly int _maxAsyncTypeId;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _disposed;

        internal AsyncContainerScope(
            IContainerScope innerScope,
            Func<Type, CancellationToken, ValueTask<object>>[] asyncFactories = null,
            int[] typeIdToAsyncIndex = null,
            int maxAsyncTypeId = -1)
        {
            _innerScope = innerScope;
            _asyncFactories = asyncFactories ?? Array.Empty<Func<Type, CancellationToken, ValueTask<object>>>();
            _typeIdToAsyncIndex = typeIdToAsyncIndex ?? Array.Empty<int>();
            _maxAsyncTypeId = maxAsyncTypeId;
        }

        public IContainer Parent => _innerScope.Parent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Resolve<T>() where T : class => _innerScope.Resolve<T>();

        public object Resolve(Type type) => _innerScope.Resolve(type);

        public bool TryResolve<T>(out T instance) where T : class => _innerScope.TryResolve(out instance);

        public bool IsRegistered<T>() where T : class => _innerScope.IsRegistered<T>();

        public bool IsRegistered(Type type) => _innerScope.IsRegistered(type);

        public IContainerScope CreateScope() => _innerScope.CreateScope();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> ResolveAsync<T>(CancellationToken cancellation = default) where T : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncContainerScope));

            int typeId = TypeRegistry.GetId<T>();

            if (typeId <= _maxAsyncTypeId && _typeIdToAsyncIndex.Length > typeId)
            {
                int asyncIndex = _typeIdToAsyncIndex[typeId];
                if (asyncIndex >= 0)
                {
                    var result = await _asyncFactories[asyncIndex](typeof(T), cancellation).ConfigureAwait(false);
                    return (T)result;
                }
            }

            var instance = _innerScope.Resolve<T>();

            if (instance is IAsyncInitializable asyncInit)
            {
                await _initLock.WaitAsync(cancellation).ConfigureAwait(false);
                try
                {
                    await asyncInit.InitializeAsync(cancellation).ConfigureAwait(false);
                }
                finally
                {
                    _initLock.Release();
                }
            }

            return instance;
        }

        public async ValueTask<object> ResolveAsync(Type type, CancellationToken cancellation = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncContainerScope));

            int typeId = TypeRegistry.GetId(type);

            if (typeId <= _maxAsyncTypeId && _typeIdToAsyncIndex.Length > typeId)
            {
                int asyncIndex = _typeIdToAsyncIndex[typeId];
                if (asyncIndex >= 0)
                    return await _asyncFactories[asyncIndex](type, cancellation).ConfigureAwait(false);
            }

            var instance = _innerScope.Resolve(type);

            if (instance is IAsyncInitializable asyncInit)
            {
                await _initLock.WaitAsync(cancellation).ConfigureAwait(false);
                try
                {
                    await asyncInit.InitializeAsync(cancellation).ConfigureAwait(false);
                }
                finally
                {
                    _initLock.Release();
                }
            }

            return instance;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _innerScope.Dispose();
            _initLock.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            if (_innerScope is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else
                _innerScope.Dispose();

            _initLock.Dispose();
        }
    }
}
