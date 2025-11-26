using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.DI.Attributes;
using Strada.Core.ECS;
using Strada.Core.MVCS.Interfaces;

namespace Strada.Core.MVCS
{
    /// <summary>
    /// Base class for all MVCS components (Controllers, Services, etc).
    /// Provides DI integration, messaging, and lifecycle management.
    /// </summary>
    public abstract class StradaBase : IInitializable, IDisposable
    {
        private readonly List<IDisposable> _disposables = new(4);
        private readonly List<Action> _unsubscribes = new(4);
        private bool _initialized;
        private bool _disposed;

        protected IContainer Container { get; private set; }
        protected World World { get; private set; }
        protected EntityManager Entities { get; private set; }
        protected StradaBus Bus { get; private set; }
        protected bool IsInitialized => _initialized;
        protected bool IsDisposed => _disposed;

        [Inject]
        public void Construct(IContainer container)
        {
            Container = container;
            container.TryResolve(out World world);
            World = world;
            Entities = world?.Entities;
            container.TryResolve(out StradaBus bus);
            Bus = bus;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnDispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T Resolve<T>() where T : class => Container?.Resolve<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool TryResolve<T>(out T instance) where T : class
        {
            if (Container == null)
            {
                instance = null;
                return false;
            }
            return Container.TryResolve(out instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Subscribe<T>(Action<T> handler) where T : struct
        {
            Bus?.Subscribe(handler);
            _unsubscribes.Add(() => Bus?.Unsubscribe(handler));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Publish<T>(T evt) where T : struct
        {
            Bus?.Publish(evt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Send<T>(T command) where T : struct
        {
            Bus?.Send(command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>
        {
            return Bus != null ? Bus.Query<TQuery, TResult>(query) : default;
        }

        protected void AddDisposable(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Entity CreateEntity() => Entities?.CreateEntity() ?? default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void DestroyEntity(Entity entity) => Entities?.DestroyEntity(entity);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            OnDispose();

            foreach (var unsub in _unsubscribes)
                unsub();
            _unsubscribes.Clear();

            foreach (var disposable in _disposables)
                disposable.Dispose();
            _disposables.Clear();

            GC.SuppressFinalize(this);
        }
    }
}
