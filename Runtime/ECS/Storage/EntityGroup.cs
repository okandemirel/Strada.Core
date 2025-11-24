using System.Collections.Generic;

namespace Strada.Core.ECS.Storage
{
    public class EntityGroup
    {
        private readonly Archetype _archetype;
        private readonly HashSet<int> _entities;

        public Archetype Archetype => _archetype;
        public int Count => _entities.Count;

        public EntityGroup(Archetype archetype)
        {
            _archetype = archetype;
            _entities = new HashSet<int>();
        }

        public void Add(int entityIndex)
        {
            _entities.Add(entityIndex);
        }

        public bool Remove(int entityIndex)
        {
            return _entities.Remove(entityIndex);
        }

        public bool Contains(int entityIndex)
        {
            return _entities.Contains(entityIndex);
        }

        public IEnumerable<int> GetEntities()
        {
            return _entities;
        }

        public void Clear()
        {
            _entities.Clear();
        }
    }
}
