using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.ECS.Core;

namespace Strada.Core.ECS.Reactive
{
    public sealed class ReactiveEntityManager : IDisposable
    {
        private readonly EntityManager _entityManager;
        private readonly Dictionary<Type, object> _reactiveStorages = new(16);

        public EntityManager Entities => _entityManager;

        public ReactiveEntityManager()
        {
            _entityManager = new EntityManager();
        }

        public ReactiveEntityManager(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReactiveComponentStorage<T> GetReactiveStorage<T>() where T : unmanaged, IComponent
        {
            var type = typeof(T);
            if (_reactiveStorages.TryGetValue(type, out var storage))
                return (ReactiveComponentStorage<T>)storage;

            var newStorage = new ReactiveComponentStorage<T>();
            _reactiveStorages[type] = newStorage;
            return newStorage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity() => _entityManager.CreateEntity();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyEntity(Entity entity)
        {
            foreach (var storage in _reactiveStorages.Values)
            {
                if (storage is IReactiveStorage reactive)
                    reactive.Remove(entity.Index);
            }
            _entityManager.DestroyEntity(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReactiveComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
        {
            var storage = GetReactiveStorage<T>();
            storage.Add(entity.Index, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetReactiveComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
        {
            var storage = GetReactiveStorage<T>();
            storage.Set(entity.Index, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveReactiveComponent<T>(Entity entity) where T : unmanaged, IComponent
        {
            var storage = GetReactiveStorage<T>();
            return storage.Remove(entity.Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetReactiveComponent<T>(Entity entity) where T : unmanaged, IComponent
        {
            var storage = GetReactiveStorage<T>();
            return storage.Get(entity.Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnAdd<T>(Action<int, T> callback) where T : unmanaged, IComponent
        {
            GetReactiveStorage<T>().SubscribeOnAdd(callback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRemove<T>(Action<int, T> callback) where T : unmanaged, IComponent
        {
            GetReactiveStorage<T>().SubscribeOnRemove(callback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnChange<T>(Action<int, T, T> callback) where T : unmanaged, IComponent
        {
            GetReactiveStorage<T>().SubscribeOnChange(callback);
        }

        public void Dispose()
        {
            foreach (var storage in _reactiveStorages.Values)
            {
                if (storage is IDisposable d)
                    d.Dispose();
            }
            _reactiveStorages.Clear();
            _entityManager.Dispose();
        }
    }
}
