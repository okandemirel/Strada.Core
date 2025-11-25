using System;
using System.Collections.Generic;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.DI.Attributes;
using Strada.Core.ECS;
using Strada.Core.MVCS.Interfaces;
using Strada.Core.Signals;

namespace Strada.Core.MVCS
{
    public abstract class StradaService : IService, IInitializable, IDisposable
    {
        private readonly List<IDisposable> _disposables = new(4);
        private readonly List<Action> _unsubscribes = new(4);
        private bool _initialized;
        private bool _disposed;

        protected IContainer Container { get; private set; }
        protected World World { get; private set; }
        protected EntityManager Entities { get; private set; }
        protected ZeroAllocEventBus EventBus { get; private set; }
        protected SignalBus Signals { get; private set; }
        protected bool IsInitialized => _initialized;

        [Inject]
        public void Construct(IContainer container)
        {
            Container = container;
            container.TryResolve(out World world);
            World = world;
            Entities = world?.Entities;
            container.TryResolve(out ZeroAllocEventBus eventBus);
            EventBus = eventBus;
            container.TryResolve(out SignalBus signals);
            Signals = signals;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnDispose() { }

        protected T Resolve<T>() where T : class => Container?.Resolve<T>();

        protected bool TryResolve<T>(out T instance) where T : class
        {
            if (Container == null)
            {
                instance = null;
                return false;
            }
            return Container.TryResolve(out instance);
        }

        protected void Subscribe<T>(Action<T> handler) where T : struct, IEventData
        {
            EventBus?.Subscribe(handler);
            _unsubscribes.Add(() => EventBus?.Unsubscribe(handler));
        }

        protected void Subscribe<T>(Signal<T> signal, Action<T> handler)
        {
            signal.AddListener(handler);
            _unsubscribes.Add(() => signal.RemoveListener(handler));
        }

        protected void Publish<T>(T evt) where T : struct, IEventData
        {
            EventBus?.Publish(evt);
        }

        protected void AddDisposable(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

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

    public abstract class TickableService : StradaService, ITickable
    {
        public virtual void Tick(float deltaTime) { }
    }

    public abstract class FixedTickableService : StradaService, IFixedTickable
    {
        public virtual void FixedTick(float fixedDeltaTime) { }
    }

    public abstract class OrderedService : StradaService
    {
        public virtual int InitializationOrder => 0;
    }
}
