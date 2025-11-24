using System;
using System.Collections.Generic;
using System.Linq;

namespace Strada.Core.ECS.Storage
{
    public class GroupRegistry : IDisposable
    {
        private readonly Dictionary<Archetype, EntityGroup> _groups;
        private readonly Dictionary<int, Archetype> _entityToArchetype;
        private readonly ComponentStore _store;

        public GroupRegistry(ComponentStore store)
        {
            _groups = new Dictionary<Archetype, EntityGroup>();
            _entityToArchetype = new Dictionary<int, Archetype>();
            _store = store;
        }

        public EntityGroup GetOrCreateGroup(Archetype archetype)
        {
            if (!_groups.TryGetValue(archetype, out var group))
            {
                group = new EntityGroup(archetype);
                _groups[archetype] = group;
            }
            return group;
        }

        public void UpdateEntityArchetype(int entityIndex)
        {
            var componentTypes = GetEntityComponentTypes(entityIndex).ToArray();
            var newArchetype = new Archetype(componentTypes);

            if (_entityToArchetype.TryGetValue(entityIndex, out var oldArchetype))
            {
                if (oldArchetype == newArchetype)
                    return;

                if (_groups.TryGetValue(oldArchetype, out var oldGroup))
                {
                    oldGroup.Remove(entityIndex);
                }
            }

            var newGroup = GetOrCreateGroup(newArchetype);
            newGroup.Add(entityIndex);
            _entityToArchetype[entityIndex] = newArchetype;
        }

        public void RemoveEntity(int entityIndex)
        {
            if (_entityToArchetype.TryGetValue(entityIndex, out var archetype))
            {
                if (_groups.TryGetValue(archetype, out var group))
                {
                    group.Remove(entityIndex);
                }
                _entityToArchetype.Remove(entityIndex);
            }
        }

        public EntityGroup GetEntityGroup(int entityIndex)
        {
            return _entityToArchetype.TryGetValue(entityIndex, out var archetype)
                ? _groups.GetValueOrDefault(archetype)
                : null;
        }

        public IEnumerable<EntityGroup> GetAllGroups()
        {
            return _groups.Values;
        }

        public IEnumerable<EntityGroup> QueryGroups(params Type[] requiredComponents)
        {
            foreach (var group in _groups.Values)
            {
                bool matches = true;
                foreach (var required in requiredComponents)
                {
                    if (!group.Archetype.ComponentTypes.Contains(required))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    yield return group;
            }
        }

        public void Clear()
        {
            foreach (var group in _groups.Values)
            {
                group.Clear();
            }
            _groups.Clear();
            _entityToArchetype.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        private IEnumerable<Type> GetEntityComponentTypes(int entityIndex)
        {
            return _store.GetComponentTypes()
                .Where(type => IsStorageContainingEntity(type, entityIndex));
        }

        private bool IsStorageContainingEntity(Type componentType, int entityIndex)
        {
            foreach (var type in _store.GetComponentTypes())
            {
                if (type == componentType)
                {
                    var storageProperty = _store.GetType().GetMethod("GetOrCreateStorage")
                        .MakeGenericMethod(componentType);
                    var storage = (IComponentStorage)storageProperty.Invoke(_store, null);
                    return storage.Contains(entityIndex);
                }
            }
            return false;
        }
    }
}
