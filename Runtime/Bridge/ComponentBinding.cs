using System;
using System.Runtime.CompilerServices;
using Strada.Core.ECS;

namespace Strada.Core.Bridge
{
    public interface IComponentBinding : IDisposable
    {
        void Sync();
        void Push();
    }

    public sealed class ComponentBinding<TComponent, TProperty> : IComponentBinding
        where TComponent : unmanaged, IComponent
    {
        private readonly EntityManager _entities;
        private readonly Func<TComponent, TProperty> _selector;
        private readonly Func<TComponent, TProperty, TComponent> _setter;
        private readonly Action<TProperty> _onChanged;
        private Entity _entity;
        private TProperty _lastValue;
        private bool _disposed;

        public TProperty Value => _lastValue;

        public ComponentBinding(
            EntityManager entities,
            Entity entity,
            Func<TComponent, TProperty> selector,
            Action<TProperty> onChanged)
        {
            _entities = entities;
            _entity = entity;
            _selector = selector;
            _onChanged = onChanged;
            _setter = null;

            if (_entities.HasComponent<TComponent>(_entity))
            {
                var component = _entities.GetComponent<TComponent>(_entity);
                _lastValue = _selector(component);
            }
        }

        public ComponentBinding(
            EntityManager entities,
            Entity entity,
            Func<TComponent, TProperty> selector,
            Func<TComponent, TProperty, TComponent> setter,
            Action<TProperty> onChanged)
        {
            _entities = entities;
            _entity = entity;
            _selector = selector;
            _setter = setter;
            _onChanged = onChanged;

            if (_entities.HasComponent<TComponent>(_entity))
            {
                var component = _entities.GetComponent<TComponent>(_entity);
                _lastValue = _selector(component);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sync()
        {
            if (!_entities.Exists(_entity)) return;
            if (!_entities.HasComponent<TComponent>(_entity)) return;

            var component = _entities.GetComponent<TComponent>(_entity);
            var newValue = _selector(component);

            if (!Equals(_lastValue, newValue))
            {
                _lastValue = newValue;
                _onChanged?.Invoke(newValue);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push()
        {
            if (_setter == null) return;
            if (!_entities.Exists(_entity)) return;
            if (!_entities.HasComponent<TComponent>(_entity)) return;

            var component = _entities.GetComponent<TComponent>(_entity);
            component = _setter(component, _lastValue);
            _entities.SetComponent(_entity, component);
        }

        public void Push(TProperty value)
        {
            _lastValue = value;
            Push();
        }

        public void Rebind(Entity entity)
        {
            _entity = entity;
            if (_entities.Exists(_entity) && _entities.HasComponent<TComponent>(_entity))
            {
                var component = _entities.GetComponent<TComponent>(_entity);
                _lastValue = _selector(component);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }

    public sealed class AutoSyncBinding<TComponent> : IComponentBinding
        where TComponent : unmanaged, IComponent
    {
        private readonly EntityManager _entities;
        private readonly Action<TComponent> _onChanged;
        private Entity _entity;
        private TComponent _lastValue;
        private bool _disposed;

        public ref readonly TComponent Value => ref _lastValue;

        public AutoSyncBinding(EntityManager entities, Entity entity, Action<TComponent> onChanged)
        {
            _entities = entities;
            _entity = entity;
            _onChanged = onChanged;

            if (_entities.HasComponent<TComponent>(_entity))
                _lastValue = _entities.GetComponent<TComponent>(_entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sync()
        {
            if (!_entities.Exists(_entity)) return;
            if (!_entities.HasComponent<TComponent>(_entity)) return;

            var current = _entities.GetComponent<TComponent>(_entity);
            if (!_lastValue.Equals(current))
            {
                _lastValue = current;
                _onChanged?.Invoke(current);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push()
        {
            if (!_entities.Exists(_entity)) return;
            _entities.SetComponent(_entity, _lastValue);
        }

        public void Push(TComponent value)
        {
            _lastValue = value;
            Push();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
