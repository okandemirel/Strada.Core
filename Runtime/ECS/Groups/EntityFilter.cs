using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.ECS.Storage;

namespace Strada.Core.ECS.Groups
{
    public readonly struct FilterId : IEquatable<FilterId>
    {
        public readonly int Value;

        public FilterId(int value) => Value = value;

        public static FilterId None => new(0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(FilterId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FilterId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(FilterId left, FilterId right) => left.Value == right.Value;
        public static bool operator !=(FilterId left, FilterId right) => left.Value != right.Value;
    }

    public sealed class FilterRegistry
    {
        private readonly Dictionary<FilterId, HashSet<int>> _filterEntities = new();
        private readonly Dictionary<FilterId, Predicate<int>> _filterPredicates = new();
        private int _nextFilterId = 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FilterId CreateFilter(Predicate<int> predicate)
        {
            var id = new FilterId(_nextFilterId++);
            _filterEntities[id] = new HashSet<int>();
            _filterPredicates[id] = predicate;
            return id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToFilter(FilterId filter, int entityIndex)
        {
            if (filter == FilterId.None) return;

            if (_filterPredicates.TryGetValue(filter, out var predicate) && !predicate(entityIndex))
                return;

            if (_filterEntities.TryGetValue(filter, out var entities))
                entities.Add(entityIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveFromFilter(FilterId filter, int entityIndex)
        {
            if (_filterEntities.TryGetValue(filter, out var entities))
                entities.Remove(entityIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveFromAllFilters(int entityIndex)
        {
            foreach (var entities in _filterEntities.Values)
                entities.Remove(entityIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInFilter(FilterId filter, int entityIndex)
        {
            return _filterEntities.TryGetValue(filter, out var entities) && entities.Contains(entityIndex);
        }

        public IReadOnlyCollection<int> GetEntitiesInFilter(FilterId filter)
        {
            return _filterEntities.TryGetValue(filter, out var entities) ? entities : Array.Empty<int>();
        }

        public int GetFilterCount(FilterId filter)
        {
            return _filterEntities.TryGetValue(filter, out var entities) ? entities.Count : 0;
        }

        public void RefreshFilter(FilterId filter, IEnumerable<int> allEntities)
        {
            if (!_filterEntities.TryGetValue(filter, out var entities))
                return;

            if (!_filterPredicates.TryGetValue(filter, out var predicate))
                return;

            entities.Clear();
            foreach (var entityIndex in allEntities)
            {
                if (predicate(entityIndex))
                    entities.Add(entityIndex);
            }
        }

        public void Clear()
        {
            foreach (var entities in _filterEntities.Values)
                entities.Clear();
        }

        public void RemoveFilter(FilterId filter)
        {
            _filterEntities.Remove(filter);
            _filterPredicates.Remove(filter);
        }
    }
}
