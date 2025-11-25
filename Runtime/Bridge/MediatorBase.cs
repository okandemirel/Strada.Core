using System;
using System.Collections.Generic;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.Signals;
using UnityEngine;

namespace Strada.Core.Bridge
{
    public abstract class MediatorBase : MonoBehaviour, IDisposable
    {
        private readonly List<IDisposable> _disposables = new(8);
        private readonly List<Action> _unsubscribes = new(8);
        private bool _initialized;
        private bool _disposed;

        protected IContainer Container { get; private set; }
        protected EntityManager EntityManager { get; private set; }
        protected ZeroAllocEventBus EventBus { get; private set; }

        public void Initialize(IContainer container)
        {
            if (_initialized) return;

            Container = container;
            Container.TryResolve(out EntityManager em);
            EntityManager = em;
            Container.TryResolve(out ZeroAllocEventBus eb);
            EventBus = eb;

            OnInitialize();
            _initialized = true;
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnDispose() { }

        protected void Subscribe<T>(Signal<T> signal, Action<T> handler)
        {
            signal.AddListener(handler);
            _unsubscribes.Add(() => signal.RemoveListener(handler));
        }

        protected void Subscribe<T>(Action<T> handler) where T : struct, IEventData
        {
            EventBus?.Subscribe(handler);
            _unsubscribes.Add(() => EventBus?.Unsubscribe(handler));
        }

        protected void AddDisposable(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        protected EntityBinding<T> BindEntity<T>(Entity entity) where T : unmanaged, IComponent
        {
            var binding = new EntityBinding<T>(EntityManager, entity);
            _disposables.Add(binding);
            return binding;
        }

        protected EntityBinding<T> BindEntity<T>(Entity entity, T initialValue) where T : unmanaged, IComponent
        {
            var binding = new EntityBinding<T>(EntityManager, entity, initialValue);
            _disposables.Add(binding);
            return binding;
        }

        protected T Resolve<T>() where T : class
        {
            return Container?.Resolve<T>();
        }

        protected bool TryResolve<T>(out T instance) where T : class
        {
            if (Container == null)
            {
                instance = null;
                return false;
            }
            return Container.TryResolve(out instance);
        }

        protected virtual void OnDestroy()
        {
            Dispose();
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
        }
    }

    public abstract class MediatorBase<TView> : MediatorBase where TView : MonoBehaviour
    {
        [SerializeField] private TView _view;

        protected TView View => _view;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            if (_view == null)
                _view = GetComponent<TView>();
        }
    }
}
