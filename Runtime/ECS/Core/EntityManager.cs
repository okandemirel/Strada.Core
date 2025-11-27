using System;
using System.Collections.Generic;
using Strada.Core.ECS.Storage;

namespace Strada.Core.ECS.Core
{
    public sealed class EntityManager : IDisposable
    {
        private int _nextEntityIndex;
        private readonly Dictionary<int, int> _entityVersions;
        private readonly HashSet<int> _activeEntities;
        private readonly ComponentStore _store;
        private readonly Queue<int> _recycledIndices;

        public int EntityCount => _activeEntities.Count;
        public ComponentStore Store => _store;

        public EntityManager()
        {
            _nextEntityIndex = 1;
            _entityVersions = new Dictionary<int, int>();
            _activeEntities = new HashSet<int>();
            _store = new ComponentStore();
            _recycledIndices = new Queue<int>();
        }

        public Entity CreateEntity()
        {
            int index = _recycledIndices.Count > 0 ? _recycledIndices.Dequeue() : _nextEntityIndex++;

            if (!_entityVersions.TryGetValue(index, out int version))
                version = 1;
            else
                version++;

            _entityVersions[index] = version;
            _activeEntities.Add(index);

            return new Entity(index, version);
        }

        public void DestroyEntity(Entity entity)
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            _store.RemoveEntity(entity.Index);
            _activeEntities.Remove(entity.Index);
            _recycledIndices.Enqueue(entity.Index);
        }

        public bool Exists(Entity entity)
        {
            if (!_activeEntities.Contains(entity.Index))
                return false;

            return _entityVersions.TryGetValue(entity.Index, out int version) && version == entity.Version;
        }

        public void AddComponent<T>(Entity entity) where T : unmanaged, IComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            var storage = _store.GetOrCreateStorage<T>();
            storage.Add(entity.Index, default);
        }

        public void AddComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            var storage = _store.GetOrCreateStorage<T>();
            storage.Add(entity.Index, component);
        }

        public void RemoveComponent<T>(Entity entity) where T : unmanaged, IComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            var storage = _store.GetOrCreateStorage<T>();
            storage.Remove(entity.Index);
        }

        public bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return false;

            var storage = _store.GetOrCreateStorage<T>();
            return storage.Contains(entity.Index);
        }

        public T GetComponent<T>(Entity entity) where T : unmanaged, IComponent
        {
            var storage = _store.GetOrCreateStorage<T>();
            return storage.Get(entity.Index);
        }

        public void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
        {
            if (!_activeEntities.Contains(entity.Index))
                return;

            var storage = _store.GetOrCreateStorage<T>();
            storage.Set(entity.Index, component);
        }

        public IEnumerable<int> GetAllEntities()
        {
            return _activeEntities;
        }

        public void Clear()
        {
            _store.Clear();
            _activeEntities.Clear();
            _entityVersions.Clear();
            _recycledIndices.Clear();
            _nextEntityIndex = 1;
        }

        public void Dispose()
        {
            _store.Dispose();
            _activeEntities.Clear();
            _entityVersions.Clear();
            _recycledIndices.Clear();
        }
    }
}
