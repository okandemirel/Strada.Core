using System;
using System.Runtime.InteropServices;

namespace Strada.Core.ECS
{
    /// <summary>
    /// Lightweight entity identifier.
    /// 64-bit value: 32-bit index + 32-bit version for safety.
    /// Designed to be Burst-compatible.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Entity : IEquatable<Entity>
    {
        public readonly int Index;
        public readonly int Version;

        public static readonly Entity Null = new Entity(0, 0);

        public Entity(int index, int version)
        {
            Index = index;
            Version = version;
        }

        public bool IsNull => Index == 0 && Version == 0;

        public bool Equals(Entity other)
        {
            return Index == other.Index && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            return obj is Entity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ Version;
            }
        }

        public static bool operator ==(Entity left, Entity right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Entity left, Entity right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"Entity({Index}, v{Version})";
        }
    }
}
