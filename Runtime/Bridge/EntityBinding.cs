using System;
using System.Runtime.CompilerServices;
using Strada.Core.ECS;
using Strada.Core.ECS.Storage;

namespace Strada.Core.Bridge
{
    public interface IEntityBinding : IDisposable
    {
        Entity Entity { get; }
        bool IsValid { get; }
        void Sync();
    }

    public sealed class EntityBinding<T> : IEntityBinding where T : unmanaged, IComponent
    {
        private readonly EntityManager _entityManager;
        private readonly ReactiveProperty<T> _property;
        private Entity _entity;
        private bool _disposed;

        public Entity Entity => _entity;
        public bool IsValid => _entityManager.Exists(_entity);
        public ReactiveProperty<T> Property => _property;

        public EntityBinding(EntityManager entityManager, Entity entity)
        {
            _entityManager = entityManager;
            _entity = entity;
            _property = new ReactiveProperty<T>();

            if (_entityManager.HasComponent<T>(_entity))
            {
                _property.SetWithoutNotify(_entityManager.GetComponent<T>(_entity));
            }
        }

        public EntityBinding(EntityManager entityManager, Entity entity, T initialValue)
        {
            _entityManager = entityManager;
            _entity = entity;
            _property = new ReactiveProperty<T>(initialValue);

            if (!_entityManager.HasComponent<T>(_entity))
            {
                _entityManager.AddComponent(_entity, initialValue);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sync()
        {
            if (!IsValid) return;

            var current = _entityManager.GetComponent<T>(_entity);
            _property.SetWithoutNotify(current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push()
        {
            if (!IsValid) return;
            _entityManager.SetComponent(_entity, _property.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(T value)
        {
            if (!IsValid) return;
            _property.Value = value;
            _entityManager.SetComponent(_entity, value);
        }

        public void Rebind(Entity newEntity)
        {
            _entity = newEntity;
            if (IsValid && _entityManager.HasComponent<T>(_entity))
            {
                Sync();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _property.Dispose();
        }
    }

}
