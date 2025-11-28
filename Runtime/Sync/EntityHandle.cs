using System;

namespace Strada.Core.Sync
{
    public readonly struct EntityHandle : IEquatable<EntityHandle>
    {
        private readonly int _id;
        private readonly int _version;

        internal EntityHandle(int id, int version)
        {
            _id = id;
            _version = version;
        }

        public bool IsValid => _id > 0;
        public static EntityHandle Invalid => default;

        internal int Id => _id;
        internal int Version => _version;

        public bool Equals(EntityHandle other) => _id == other._id && _version == other._version;
        public override bool Equals(object obj) => obj is EntityHandle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_id, _version);
        public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);
        public static bool operator !=(EntityHandle left, EntityHandle right) => !left.Equals(right);
    }
}
