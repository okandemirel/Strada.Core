using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.ECS.Storage;

namespace Strada.Core.ECS.Query
{
    public struct QueryDescriptor : IEquatable<QueryDescriptor>
    {
        public Type[] RequiredComponents;
        public Type[] ExcludedComponents;

        public QueryDescriptor(Type[] required, Type[] excluded = null)
        {
            RequiredComponents = required?.OrderBy(t => t.GetHashCode()).ToArray() ?? Array.Empty<Type>();
            ExcludedComponents = excluded?.OrderBy(t => t.GetHashCode()).ToArray() ?? Array.Empty<Type>();
        }

        public bool Equals(QueryDescriptor other)
        {
            return RequiredComponents.SequenceEqual(other.RequiredComponents) &&
                   ExcludedComponents.SequenceEqual(other.ExcludedComponents);
        }

        public override bool Equals(object obj)
        {
            return obj is QueryDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var type in RequiredComponents)
                    hash = hash * 31 + type.GetHashCode();
                foreach (var type in ExcludedComponents)
                    hash = hash * 31 + type.GetHashCode();
                return hash;
            }
        }
    }

    public class EntityQuery
    {
        private readonly QueryDescriptor _descriptor;
        private readonly ComponentStore _store;
        private List<int> _cachedEntities;
        private bool _isDirty;

        public QueryDescriptor Descriptor => _descriptor;
        public int EntityCount => _cachedEntities?.Count ?? 0;

        public EntityQuery(QueryDescriptor descriptor, ComponentStore store)
        {
            _descriptor = descriptor;
            _store = store;
            _cachedEntities = new List<int>();
            _isDirty = true;
        }

        public void Invalidate()
        {
            _isDirty = true;
        }

        public IReadOnlyList<int> GetEntities()
        {
            if (_isDirty)
            {
                RebuildCache();
            }
            return _cachedEntities;
        }

        private void RebuildCache()
        {
            _cachedEntities.Clear();

            if (_descriptor.RequiredComponents.Length == 0)
            {
                _isDirty = false;
                return;
            }

            var firstComponentType = _descriptor.RequiredComponents[0];
            var firstStorage = GetStorage(firstComponentType);

            if (firstStorage == null)
            {
                _isDirty = false;
                return;
            }

            foreach (var entityIndex in firstStorage.GetEntityIndices())
            {
                if (MatchesQuery(entityIndex))
                {
                    _cachedEntities.Add(entityIndex);
                }
            }

            _isDirty = false;
        }

        private bool MatchesQuery(int entityIndex)
        {
            foreach (var required in _descriptor.RequiredComponents)
            {
                var storage = GetStorage(required);
                if (storage == null || !storage.Contains(entityIndex))
                    return false;
            }

            foreach (var excluded in _descriptor.ExcludedComponents)
            {
                var storage = GetStorage(excluded);
                if (storage != null && storage.Contains(entityIndex))
                    return false;
            }

            return true;
        }

        private IComponentStorage GetStorage(Type componentType)
        {
            var method = _store.GetType().GetMethod("GetOrCreateStorage");
            var genericMethod = method.MakeGenericMethod(componentType);
            return (IComponentStorage)genericMethod.Invoke(_store, null);
        }
    }

    public class QueryCache
    {
        private readonly Dictionary<QueryDescriptor, EntityQuery> _queries;
        private readonly ComponentStore _store;

        public QueryCache(ComponentStore store)
        {
            _queries = new Dictionary<QueryDescriptor, EntityQuery>();
            _store = store;
        }

        public EntityQuery GetOrCreateQuery(QueryDescriptor descriptor)
        {
            if (!_queries.TryGetValue(descriptor, out var query))
            {
                query = new EntityQuery(descriptor, _store);
                _queries[descriptor] = query;
            }
            return query;
        }

        public EntityQuery Query(params Type[] requiredComponents)
        {
            var descriptor = new QueryDescriptor(requiredComponents);
            return GetOrCreateQuery(descriptor);
        }

        public EntityQuery QueryWithExclusions(Type[] required, Type[] excluded)
        {
            var descriptor = new QueryDescriptor(required, excluded);
            return GetOrCreateQuery(descriptor);
        }

        public void InvalidateAll()
        {
            foreach (var query in _queries.Values)
            {
                query.Invalidate();
            }
        }

        public void InvalidateQueriesWithComponent(Type componentType)
        {
            foreach (var kvp in _queries)
            {
                if (kvp.Key.RequiredComponents.Contains(componentType) ||
                    kvp.Key.ExcludedComponents.Contains(componentType))
                {
                    kvp.Value.Invalidate();
                }
            }
        }

        public void OnStructuralChange()
        {
            InvalidateAll();
        }

        public void Clear()
        {
            _queries.Clear();
        }
    }
}
