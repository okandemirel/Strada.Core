using System;
using System.Collections.Generic;

namespace Strada.Core.ECS.Query
{
    internal readonly struct QuerySignature : IEquatable<QuerySignature>
    {
        private readonly int _hash;
        private readonly Type[] _types;

        public QuerySignature(params Type[] types)
        {
            _types = types;
            _hash = ComputeHash(types);
        }

        public bool Equals(QuerySignature other)
        {
            return _hash == other._hash;
        }

        public override bool Equals(object obj)
        {
            return obj is QuerySignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        private static int ComputeHash(Type[] types)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < types.Length; i++)
                {
                    hash = hash * 31 + types[i].GetHashCode();
                }
                return hash;
            }
        }
    }

    internal sealed class CachedQuery
    {
        public EntityQuery Query;
        public bool IsValid;

        public CachedQuery(EntityQuery query)
        {
            Query = query;
            IsValid = true;
        }

        public void Invalidate()
        {
            IsValid = false;
            Query.Invalidate();
        }
    }

    public sealed class OptimizedQueryCache
    {
        private readonly Dictionary<QuerySignature, CachedQuery> _cache;
        private readonly Dictionary<Type, HashSet<QuerySignature>> _componentToQueries;
        private readonly Storage.ComponentStore _store;

        public OptimizedQueryCache(Storage.ComponentStore store)
        {
            _store = store;
            _cache = new Dictionary<QuerySignature, CachedQuery>();
            _componentToQueries = new Dictionary<Type, HashSet<QuerySignature>>();
        }

        public EntityQuery Query(params Type[] componentTypes)
        {
            var signature = new QuerySignature(componentTypes);

            if (_cache.TryGetValue(signature, out var cached) && cached.IsValid)
                return cached.Query;

            var query = BuildQuery(componentTypes);
            var cachedQuery = new CachedQuery(query);
            _cache[signature] = cachedQuery;

            foreach (var type in componentTypes)
            {
                if (!_componentToQueries.TryGetValue(type, out var queries))
                {
                    queries = new HashSet<QuerySignature>();
                    _componentToQueries[type] = queries;
                }
                queries.Add(signature);
            }

            return query;
        }

        public void InvalidateQueriesWithComponent(Type componentType)
        {
            if (!_componentToQueries.TryGetValue(componentType, out var queries))
                return;

            foreach (var signature in queries)
            {
                if (_cache.TryGetValue(signature, out var cached))
                    cached.Invalidate();
            }
        }

        public void Clear()
        {
            _cache.Clear();
            _componentToQueries.Clear();
        }

        private EntityQuery BuildQuery(Type[] componentTypes)
        {
            var descriptor = new QueryDescriptor(componentTypes);
            return new EntityQuery(descriptor, _store);
        }
    }
}
