using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.MVCS;

namespace Strada.Core.Bridge
{
    public abstract class ViewMediator<TView> : IDisposable where TView : StradaView
    {
        private readonly List<IComponentBinding> _bindings = new(8);
        private readonly List<IDisposable> _disposables = new(4);
        private EntityManager _entities;
        private IContainer _container;
        private TView _view;
        private Entity _entity;
        private bool _bound;
        private bool _disposed;

        protected EntityManager Entities => _entities;
        protected IContainer Container => _container;
        protected TView View => _view;
        protected Entity Entity => _entity;
        public bool IsBound => _bound;

        public void Initialize(IContainer container)
        {
            _container = container;
            _container.TryResolve(out _entities);
            OnInitialize();
        }

        public void Bind(Entity entity, TView view)
        {
            if (_bound) Unbind();

            _entity = entity;
            _view = view;
            _bound = true;

            OnBind();
        }

        public void Unbind()
        {
            if (!_bound) return;

            OnUnbind();

            foreach (var binding in _bindings)
                binding.Dispose();
            _bindings.Clear();

            _entity = default;
            _view = null;
            _bound = false;
        }

        public void SyncAll()
        {
            if (!_bound) return;
            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].Sync();
        }

        public void PushAll()
        {
            if (!_bound) return;
            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].Push();
        }

        protected virtual void OnInitialize() { }
        protected abstract void OnBind();
        protected abstract void OnUnbind();

        protected ComponentBinding<TComponent, TProperty> Bind<TComponent, TProperty>(
            Func<TComponent, TProperty> selector,
            Action<TProperty> onChanged)
            where TComponent : unmanaged, IComponent
        {
            var binding = new ComponentBinding<TComponent, TProperty>(_entities, _entity, selector, onChanged);
            _bindings.Add(binding);
            return binding;
        }

        protected ComponentBinding<TComponent, TProperty> Bind<TComponent, TProperty>(
            Func<TComponent, TProperty> selector,
            Func<TComponent, TProperty, TComponent> setter,
            Action<TProperty> onChanged)
            where TComponent : unmanaged, IComponent
        {
            var binding = new ComponentBinding<TComponent, TProperty>(_entities, _entity, selector, setter, onChanged);
            _bindings.Add(binding);
            return binding;
        }

        protected AutoSyncBinding<TComponent> AutoSync<TComponent>(Action<TComponent> onChanged)
            where TComponent : unmanaged, IComponent
        {
            var binding = new AutoSyncBinding<TComponent>(_entities, _entity, onChanged);
            _bindings.Add(binding);
            return binding;
        }

        protected T Resolve<T>() where T : class => _container?.Resolve<T>();

        protected void AddDisposable(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Unbind();

            foreach (var d in _disposables)
                d.Dispose();
            _disposables.Clear();

            OnDispose();
        }

        protected virtual void OnDispose() { }
    }
}
