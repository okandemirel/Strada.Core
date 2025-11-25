using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.ECS.Archetypes
{
    public abstract class EntityDescriptor : IEntityDescriptor
    {
        private readonly List<Type> _componentTypes = new(8);
        private readonly List<IComponentInitializer> _initializers = new(8);
        private Type[] _cachedTypes;

        public Type[] ComponentTypes => _cachedTypes ??= _componentTypes.ToArray();

        protected EntityDescriptor()
        {
            Define();
            _cachedTypes = _componentTypes.ToArray();
        }

        protected abstract void Define();

        protected void Add<T>() where T : unmanaged, IComponent
        {
            _componentTypes.Add(typeof(T));
            _initializers.Add(new DefaultInitializer<T>());
        }

        protected void Add<T>(T defaultValue) where T : unmanaged, IComponent
        {
            _componentTypes.Add(typeof(T));
            _initializers.Add(new ValueInitializer<T>(defaultValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InitializeComponents(EntityManager manager, Entity entity)
        {
            for (int i = 0; i < _initializers.Count; i++)
                _initializers[i].Initialize(manager, entity);
        }

        private interface IComponentInitializer
        {
            void Initialize(EntityManager manager, Entity entity);
        }

        private sealed class DefaultInitializer<T> : IComponentInitializer where T : unmanaged, IComponent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Initialize(EntityManager manager, Entity entity)
            {
                manager.AddComponent<T>(entity);
            }
        }

        private sealed class ValueInitializer<T> : IComponentInitializer where T : unmanaged, IComponent
        {
            private readonly T _value;

            public ValueInitializer(T value) => _value = value;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Initialize(EntityManager manager, Entity entity)
            {
                manager.AddComponent(entity, _value);
            }
        }
    }
}
