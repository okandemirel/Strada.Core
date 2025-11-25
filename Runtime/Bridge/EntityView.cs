using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.DI;
using Strada.Core.ECS;
using UnityEngine;

namespace Strada.Core.Bridge
{
    public interface IComponentBinding : IDisposable
    {
        void Sync();
        void Push();
        bool IsDirty { get; }
    }

    public abstract class EntityView : MonoBehaviour, IDisposable
    {
        private Entity _entity;
        private EntityManager _entityManager;
        private IContainer _container;
        private List<IComponentBinding> _bindings;
        private bool _bound;
        private bool _disposed;

        public Entity Entity => _entity;
        public bool IsBound => _bound;

        protected EntityManager EntityManager => _entityManager;
        protected IContainer Container => _container;

        public void Bind(IContainer container, EntityManager entityManager, Entity entity)
        {
            if (_bound) return;

            _container = container;
            _entityManager = entityManager;
            _entity = entity;
            _bindings = new List<IComponentBinding>(4);
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
            _entityManager = null;
            _container = null;
            _bound = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SyncBindings()
        {
            if (!_bound) return;

            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].IsDirty)
                    _bindings[i].Sync();
            }
        }

        protected abstract void OnBind();

        protected virtual void OnUnbind() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ComponentBinding<T> BindComponent<T>() where T : unmanaged, IComponent
        {
            var binding = new ComponentBinding<T>(_entityManager, _entity);
            _bindings.Add(binding);
            return binding;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ComponentBinding<T> BindComponent<T>(T initialValue) where T : unmanaged, IComponent
        {
            var binding = new ComponentBinding<T>(_entityManager, _entity, initialValue);
            _bindings.Add(binding);
            return binding;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AddBinding(IComponentBinding binding)
        {
            _bindings.Add(binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T GetComponent<T>() where T : unmanaged, IComponent
        {
            return _entityManager.GetComponent<T>(_entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetComponent<T>(T component) where T : unmanaged, IComponent
        {
            _entityManager.SetComponent(_entity, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool HasComponent<T>() where T : unmanaged, IComponent
        {
            return _entityManager.HasComponent<T>(_entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T Resolve<T>() where T : class
        {
            return _container?.Resolve<T>();
        }

        protected virtual void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Unbind();
            GC.SuppressFinalize(this);
        }
    }

    public abstract class EntityView<TComponent> : EntityView where TComponent : unmanaged, IComponent
    {
        protected ComponentBinding<TComponent> PrimaryBinding { get; private set; }

        protected override void OnBind()
        {
            PrimaryBinding = BindComponent<TComponent>();
            PrimaryBinding.OnChanged += OnComponentChanged;
        }

        protected override void OnUnbind()
        {
            if (PrimaryBinding != null)
                PrimaryBinding.OnChanged -= OnComponentChanged;
        }

        protected virtual void OnComponentChanged(TComponent component) { }
    }

    public sealed class ComponentBinding<T> : IComponentBinding where T : unmanaged, IComponent
    {
        private EntityManager _entityManager;
        private Entity _entity;
        private T _cachedValue;
        private bool _dirty;
        private bool _disposed;

        public event Action<T> OnChanged;

        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cachedValue;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _cachedValue = value;
                _entityManager.SetComponent(_entity, value);
                OnChanged?.Invoke(value);
            }
        }

        public bool IsDirty => _dirty;

        public ComponentBinding(EntityManager entityManager, Entity entity)
        {
            _entityManager = entityManager;
            _entity = entity;

            if (_entityManager.HasComponent<T>(entity))
                _cachedValue = _entityManager.GetComponent<T>(entity);
        }

        public ComponentBinding(EntityManager entityManager, Entity entity, T initialValue)
        {
            _entityManager = entityManager;
            _entity = entity;
            _cachedValue = initialValue;

            if (!_entityManager.HasComponent<T>(entity))
                _entityManager.AddComponent(entity, initialValue);
            else
                _entityManager.SetComponent(entity, initialValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sync()
        {
            if (!_entityManager.HasComponent<T>(_entity))
                return;

            var current = _entityManager.GetComponent<T>(_entity);
            _cachedValue = current;
            _dirty = false;
            OnChanged?.Invoke(current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkDirty()
        {
            _dirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write()
        {
            _entityManager.SetComponent(_entity, _cachedValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push()
        {
            _entityManager.SetComponent(_entity, _cachedValue);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            OnChanged = null;
            _entityManager = null;
        }
    }
}
