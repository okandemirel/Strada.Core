using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Strada.Core.ECS.Storage
{
    public interface IComponentStorage : IDisposable
    {
        bool Contains(int entityIndex);
        bool Remove(int entityIndex);
        void Clear();
        int Count { get; }
        IReadOnlyList<int> GetEntityIndices();
    }

    public class ComponentStorage<T> : IComponentStorage where T : unmanaged, IComponent
    {
        private SparseSet<T> _sparseSet;

        public int Count => _sparseSet.Count;

        public ComponentStorage(int sparseCapacity = 1024, int denseCapacity = 256)
        {
            _sparseSet = new SparseSet<T>(sparseCapacity, denseCapacity, Allocator.Persistent);
        }

        public void Add(int entityIndex, T component)
        {
            _sparseSet.Add(entityIndex, component);
        }

        public bool Remove(int entityIndex)
        {
            return _sparseSet.Remove(entityIndex);
        }

        public bool Contains(int entityIndex)
        {
            return _sparseSet.Contains(entityIndex);
        }

        public T Get(int entityIndex)
        {
            return _sparseSet.Get(entityIndex);
        }

        public bool TryGet(int entityIndex, out T component)
        {
            return _sparseSet.TryGet(entityIndex, out component);
        }

        public void Set(int entityIndex, T component)
        {
            _sparseSet.Set(entityIndex, component);
        }

        public ref SparseSet<T> GetSparseSet()
        {
            return ref _sparseSet;
        }

        public IReadOnlyList<int> GetEntityIndices()
        {
            var indices = new List<int>(_sparseSet.Count);
            unsafe
            {
                int* densePtr = _sparseSet.GetDenseEntityReadOnlyPtr();
                for (int i = 0; i < _sparseSet.Count; i++)
                {
                    indices.Add(densePtr[i]);
                }
            }
            return indices;
        }

        public void Clear()
        {
            _sparseSet.Clear();
        }

        public void Dispose()
        {
            _sparseSet.Dispose();
        }
    }

    public class ComponentStore : IDisposable
    {
        private readonly Dictionary<Type, IComponentStorage> _storages;
        private readonly int _defaultSparseCapacity;
        private readonly int _defaultDenseCapacity;

        public ComponentStore(int defaultSparseCapacity = 1024, int defaultDenseCapacity = 256)
        {
            _storages = new Dictionary<Type, IComponentStorage>();
            _defaultSparseCapacity = defaultSparseCapacity;
            _defaultDenseCapacity = defaultDenseCapacity;
        }

        public ComponentStorage<T> GetOrCreateStorage<T>() where T : unmanaged, IComponent
        {
            Type type = typeof(T);
            if (!_storages.TryGetValue(type, out var storage))
            {
                storage = new ComponentStorage<T>(_defaultSparseCapacity, _defaultDenseCapacity);
                _storages[type] = storage;
            }
            return (ComponentStorage<T>)storage;
        }

        public bool HasStorage<T>() where T : unmanaged, IComponent
        {
            return _storages.ContainsKey(typeof(T));
        }

        public void RemoveEntity(int entityIndex)
        {
            foreach (var storage in _storages.Values)
            {
                storage.Remove(entityIndex);
            }
        }

        public void Clear()
        {
            foreach (var storage in _storages.Values)
            {
                storage.Clear();
            }
        }

        public void Dispose()
        {
            foreach (var storage in _storages.Values)
            {
                storage.Dispose();
            }
            _storages.Clear();
        }

        public IEnumerable<Type> GetComponentTypes()
        {
            return _storages.Keys;
        }

        public int GetEntityComponentCount(int entityIndex)
        {
            int count = 0;
            foreach (var storage in _storages.Values)
            {
                if (storage.Contains(entityIndex))
                    count++;
            }
            return count;
        }

        public bool HasComponent(int entityIndex, Type componentType)
        {
            return _storages.TryGetValue(componentType, out var storage) && storage.Contains(entityIndex);
        }

        public object GetComponentBoxed(int entityIndex, Type componentType)
        {
            if (!_storages.TryGetValue(componentType, out var storage))
                return null;

            var method = storage.GetType().GetMethod("Get");
            if (method == null) return null;

            try
            {
                return method.Invoke(storage, new object[] { entityIndex });
            }
            catch
            {
                return null;
            }
        }

        public void SetComponentBoxed(int entityIndex, Type componentType, object value)
        {
            if (!_storages.TryGetValue(componentType, out var storage))
                return;

            var method = storage.GetType().GetMethod("Set");
            if (method == null) return;

            try
            {
                method.Invoke(storage, new object[] { entityIndex, value });
            }
            catch
            {
            }
        }
    }
}
