using System;

namespace Strada.Core.ECS
{
    public interface IStradaSystem
    {
        void OnCreate(ref SystemState state);
        void OnUpdate(ref SystemState state);
        void OnDestroy(ref SystemState state);
    }

    public struct SystemState
    {
        public IEntityManager EntityManager { get; internal set; }
        public float DeltaTime { get; internal set; }
        public double Time { get; internal set; }
        public bool Enabled { get; set; }
    }

    public interface IEntityManager
    {
        Entity CreateEntity();
        Entity CreateEntity(EntityArchetype archetype);
        void DestroyEntity(Entity entity);
        bool Exists(Entity entity);
        void AddComponent<T>(Entity entity) where T : unmanaged, IStradaComponent;
        void RemoveComponent<T>(Entity entity) where T : unmanaged, IStradaComponent;
        bool HasComponent<T>(Entity entity) where T : unmanaged, IStradaComponent;
        T GetComponent<T>(Entity entity) where T : unmanaged, IStradaComponent;
        void SetComponent<T>(Entity entity, T component) where T : unmanaged, IStradaComponent;
    }

    public struct Entity : IEquatable<Entity>
    {
        public int Index;
        public int Version;

        public static Entity Null => new Entity { Index = 0, Version = 0 };

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
    }

    public struct EntityArchetype : IEquatable<EntityArchetype>
    {
        internal int Index;

        public bool Equals(EntityArchetype other)
        {
            return Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityArchetype other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Index;
        }

        public static bool operator ==(EntityArchetype left, EntityArchetype right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EntityArchetype left, EntityArchetype right)
        {
            return !left.Equals(right);
        }
    }
}
