using System;
using System.Collections.Generic;
using Strada.Core.ECS;
using Strada.Core.MVCS;

namespace Strada.Core.Bridge
{
    public abstract class EntityMediator<TView> : IDisposable where TView : StradaView
    {
        private readonly List<IComponentBinding> _bindings = new(8);
        private readonly List<IDisposable> _disposables = new(4);
        private World _world;
        private TView _view;
        private Entity _entity;
        private bool _bound;
        private bool _disposed;

        protected World World => _world;
        protected TView View => _view;
        protected Entity Entity => _entity;
        protected EntityManager Entities => _world?.Entities;
        public bool IsBound => _bound;

        public void Bind(World world, Entity entity, TView view)
        {
            if (_bound) Unbind();

            _world = world;
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

            _world = null;
            _view = null;
            _entity = default;
            _bound = false;
        }

        public void SyncBindings()
        {
            if (!_bound) return;
            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].Sync();
        }

        public void PushBindings()
        {
            if (!_bound) return;
            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].Push();
        }

        public void UpdateMediator(float deltaTime)
        {
            if (!_bound) return;
            SyncBindings();
            OnUpdate(deltaTime);
        }

        protected abstract void OnBind();
        protected abstract void OnUnbind();
        protected virtual void OnUpdate(float deltaTime) { }

        protected ComponentBinding<TComponent, TProperty> Bind<TComponent, TProperty>(
            Func<TComponent, TProperty> selector,
            Action<TProperty> onChanged)
            where TComponent : unmanaged, IComponent
        {
            var binding = new ComponentBinding<TComponent, TProperty>(Entities, _entity, selector, onChanged);
            _bindings.Add(binding);
            return binding;
        }

        protected ComponentBinding<TComponent, TProperty> Bind<TComponent, TProperty>(
            Func<TComponent, TProperty> selector,
            Func<TComponent, TProperty, TComponent> setter,
            Action<TProperty> onChanged)
            where TComponent : unmanaged, IComponent
        {
            var binding = new ComponentBinding<TComponent, TProperty>(Entities, _entity, selector, setter, onChanged);
            _bindings.Add(binding);
            return binding;
        }

        protected AutoSyncBinding<TComponent> AutoSync<TComponent>(Action<TComponent> onChanged)
            where TComponent : unmanaged, IComponent
        {
            var binding = new AutoSyncBinding<TComponent>(Entities, _entity, onChanged);
            _bindings.Add(binding);
            return binding;
        }

        protected T GetComponent<T>() where T : unmanaged, IComponent
            => _world.GetComponent<T>(_entity);

        protected void SetComponent<T>(T component) where T : unmanaged, IComponent
            => _world.SetComponent(_entity, component);

        protected bool HasComponent<T>() where T : unmanaged, IComponent
            => _world.HasComponent<T>(_entity);

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
