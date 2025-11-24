using System;
using System.Collections.Generic;
using Strada.Core.ECS.Storage;
using Strada.Core.ECS.Query;

namespace Strada.Core.ECS
{
    public class EntityManager : IEntityManager, IDisposable
    {
        private int _nextEntityIndex;
        private readonly HashSet<int> _activeEntities;
        private readonly ComponentStore _store;
        private readonly GroupRegistry _groups;
        private readonly QueryCache _queryCache;
        private readonly Queue<int> _recycledIndices;

        public int EntityCount => _activeEntities.Count;
        public ComponentStore Store => _store;
        public QueryCache QueryCache => _queryCache;

        public EntityManager()
        {
            _nextEntityIndex = 1;
            _activeEntities = new HashSet<int>();
            _store = new ComponentStore();
            _groups = new GroupRegistry(_store);
            _queryCache = new QueryCache(_store);
            _recycledIndices = new Queue<int>();
        }

        public Entity CreateEntity()
        {
            int index = _recycledIndices.Count > 0 ? _recycledIndices.Dequeue() : _nextEntityIndex++;
            _activeEntities.Add(index);
            return new Entity { Index = index, Version = 1 };
        }

        public Entity CreateEntity(EntityArchetype archetype)
        {
            return CreateEntity();
        }

        public void DestroyEntity(Entity entity)
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            _store.RemoveEntity(entity.Index);
            _groups.RemoveEntity(entity.Index);
            _activeEntities.Remove(entity.Index);
            _recycledIndices.Enqueue(entity.Index);

            _queryCache.OnStructuralChange();
        }

        public bool Exists(Entity entity)
        {
            return _activeEntities.Contains(entity.Index);
        }

        public void AddComponent<T>(Entity entity) where T : unmanaged, IStradaComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            var storage = _store.GetOrCreateStorage<T>();
            storage.Add(entity.Index, default);
            _groups.UpdateEntityArchetype(entity.Index);

            _queryCache.InvalidateQueriesWithComponent(typeof(T));
        }

        public void AddComponent<T>(Entity entity, T component) where T : unmanaged, IStradaComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            var storage = _store.GetOrCreateStorage<T>();
            storage.Add(entity.Index, component);
            _groups.UpdateEntityArchetype(entity.Index);

            _queryCache.InvalidateQueriesWithComponent(typeof(T));
        }

        public void RemoveComponent<T>(Entity entity) where T : unmanaged, IStradaComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            var storage = _store.GetOrCreateStorage<T>();
            storage.Remove(entity.Index);
            _groups.UpdateEntityArchetype(entity.Index);

            _queryCache.InvalidateQueriesWithComponent(typeof(T));
        }

        public bool HasComponent<T>(Entity entity) where T : unmanaged, IStradaComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return false;

            var storage = _store.GetOrCreateStorage<T>();
            return storage.Contains(entity.Index);
        }

        public T GetComponent<T>(Entity entity) where T : unmanaged, IStradaComponent
        {
            var storage = _store.GetOrCreateStorage<T>();
            return storage.Get(entity.Index);
        }

        public void SetComponent<T>(Entity entity, T component) where T : unmanaged, IStradaComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            var storage = _store.GetOrCreateStorage<T>();
            storage.Set(entity.Index, component);
        }

        public EntityQuery Query<T1>() where T1 : unmanaged, IStradaComponent
        {
            return _queryCache.Query(typeof(T1));
        }

        public EntityQuery Query<T1, T2>()
            where T1 : unmanaged, IStradaComponent
            where T2 : unmanaged, IStradaComponent
        {
            return _queryCache.Query(typeof(T1), typeof(T2));
        }

        public EntityQuery Query<T1, T2, T3>()
            where T1 : unmanaged, IStradaComponent
            where T2 : unmanaged, IStradaComponent
            where T3 : unmanaged, IStradaComponent
        {
            return _queryCache.Query(typeof(T1), typeof(T2), typeof(T3));
        }

        public IEnumerable<int> GetAllEntities()
        {
            return _activeEntities;
        }

        public void Clear()
        {
            _store.Clear();
            _groups.Clear();
            _queryCache.Clear();
            _activeEntities.Clear();
            _recycledIndices.Clear();
            _nextEntityIndex = 1;
        }

        public void Dispose()
        {
            _store.Dispose();
            _groups.Dispose();
            _queryCache.Clear();
            _activeEntities.Clear();
            _recycledIndices.Clear();
        }
    }
}
