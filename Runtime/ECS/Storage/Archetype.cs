using System;
using System.Linq;

namespace Strada.Core.ECS.Storage
{
    public struct Archetype : IEquatable<Archetype>
    {
        private readonly int _hash;
        private readonly Type[] _componentTypes;

        public Archetype(params Type[] componentTypes)
        {
            _componentTypes = componentTypes.OrderBy(t => t.GetHashCode()).ToArray();
            _hash = ComputeHash(_componentTypes);
        }

        public Type[] ComponentTypes => _componentTypes;
        public int Hash => _hash;

        private static int ComputeHash(Type[] types)
        {
            unchecked
            {
                int hash = 17;
                foreach (var type in types)
                {
                    hash = hash * 31 + type.GetHashCode();
                }
                return hash;
            }
        }

        public bool Equals(Archetype other)
        {
            return _hash == other._hash;
        }

        public override bool Equals(object obj)
        {
            return obj is Archetype other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        public static bool operator ==(Archetype left, Archetype right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Archetype left, Archetype right)
        {
            return !left.Equals(right);
        }
    }
}
