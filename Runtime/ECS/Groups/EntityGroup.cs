using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.ECS.Groups
{
    public readonly struct GroupId : IEquatable<GroupId>
    {
        public readonly int Value;

        public GroupId(int value) => Value = value;

        public static GroupId None => new(0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(GroupId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GroupId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(GroupId left, GroupId right) => left.Value == right.Value;
        public static bool operator !=(GroupId left, GroupId right) => left.Value != right.Value;
    }

    public sealed class GroupRegistry
    {
        private readonly Dictionary<Type, GroupId> _typeToGroup = new();
        private readonly Dictionary<GroupId, HashSet<int>> _groupEntities = new();
        private readonly Dictionary<int, GroupId> _entityToGroup = new();
        private int _nextGroupId = 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GroupId GetOrCreate<TGroup>() where TGroup : struct
        {
            var type = typeof(TGroup);
            if (_typeToGroup.TryGetValue(type, out var id))
                return id;

            id = new GroupId(_nextGroupId++);
            _typeToGroup[type] = id;
            _groupEntities[id] = new HashSet<int>();
            return id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GroupId GetGroup<TGroup>() where TGroup : struct
        {
            return _typeToGroup.TryGetValue(typeof(TGroup), out var id) ? id : GroupId.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToGroup(int entityIndex, GroupId group)
        {
            if (group == GroupId.None) return;

            if (_entityToGroup.TryGetValue(entityIndex, out var currentGroup))
            {
                if (currentGroup == group) return;
                _groupEntities[currentGroup].Remove(entityIndex);
            }

            _entityToGroup[entityIndex] = group;

            if (!_groupEntities.TryGetValue(group, out var entities))
            {
                entities = new HashSet<int>();
                _groupEntities[group] = entities;
            }

            entities.Add(entityIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToGroup<TGroup>(int entityIndex) where TGroup : struct
        {
            var group = GetOrCreate<TGroup>();
            AddToGroup(entityIndex, group);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveFromGroup(int entityIndex)
        {
            if (!_entityToGroup.TryGetValue(entityIndex, out var group))
                return;

            _groupEntities[group].Remove(entityIndex);
            _entityToGroup.Remove(entityIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SwapGroup(int entityIndex, GroupId fromGroup, GroupId toGroup)
        {
            if (_entityToGroup.TryGetValue(entityIndex, out var current) && current == fromGroup)
            {
                _groupEntities[fromGroup].Remove(entityIndex);
            }

            AddToGroup(entityIndex, toGroup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SwapGroup<TFrom, TTo>(int entityIndex) where TFrom : struct where TTo : struct
        {
            var fromGroup = GetOrCreate<TFrom>();
            var toGroup = GetOrCreate<TTo>();
            SwapGroup(entityIndex, fromGroup, toGroup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInGroup(int entityIndex, GroupId group)
        {
            return _entityToGroup.TryGetValue(entityIndex, out var current) && current == group;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInGroup<TGroup>(int entityIndex) where TGroup : struct
        {
            var group = GetGroup<TGroup>();
            return group != GroupId.None && IsInGroup(entityIndex, group);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GroupId GetEntityGroup(int entityIndex)
        {
            return _entityToGroup.TryGetValue(entityIndex, out var group) ? group : GroupId.None;
        }

        public IReadOnlyCollection<int> GetEntitiesInGroup(GroupId group)
        {
            return _groupEntities.TryGetValue(group, out var entities) ? entities : Array.Empty<int>();
        }

        public IReadOnlyCollection<int> GetEntitiesInGroup<TGroup>() where TGroup : struct
        {
            var group = GetGroup<TGroup>();
            return group != GroupId.None ? GetEntitiesInGroup(group) : Array.Empty<int>();
        }

        public int GetGroupCount(GroupId group)
        {
            return _groupEntities.TryGetValue(group, out var entities) ? entities.Count : 0;
        }

        public void Clear()
        {
            _entityToGroup.Clear();
            foreach (var entities in _groupEntities.Values)
                entities.Clear();
        }
    }
}
