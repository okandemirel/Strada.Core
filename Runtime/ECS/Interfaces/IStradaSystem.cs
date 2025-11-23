using System;

namespace Strada.Core.ECS
{
    /// <summary>
    /// Base interface for all Strada ECS systems.
    /// Systems contain logic that operates on entities with specific component combinations.
    /// </summary>
    /// <remarks>
    /// Strada systems wrap Unity DOTS ISystem.
    /// They should be partial structs for optimal performance.
    ///
    /// Systems are automatically discovered and registered using the [StradaSystem] attribute.
    ///
    /// Best Practices:
    /// - Use partial struct for systems (value types, better performance)
    /// - Mark systems with [StradaSystem] attribute
    /// - Specify update group with UpdateInGroup parameter
    /// - Use Burst compilation for performance
    /// - Keep systems focused on single responsibility
    ///
    /// Example:
    /// <code>
    /// [StradaSystem(UpdateInGroup = typeof(SimulationSystemGroup))]
    /// [BurstCompile]
    /// public partial struct MovementSystem : IStradaSystem
    /// {
    ///     public void OnCreate(ref SystemState state) { }
    ///     public void OnUpdate(ref SystemState state) { }
    ///     public void OnDestroy(ref SystemState state) { }
    /// }
    /// </code>
    /// </remarks>
    public interface IStradaSystem
    {
        /// <summary>
        /// Called once when the system is created.
        /// Use this for one-time initialization.
        /// </summary>
        /// <param name="state">The system state</param>
        void OnCreate(ref SystemState state);

        /// <summary>
        /// Called every frame when the system updates.
        /// This is where the main system logic goes.
        /// </summary>
        /// <param name="state">The system state</param>
        void OnUpdate(ref SystemState state);

        /// <summary>
        /// Called once when the system is destroyed.
        /// Use this for cleanup.
        /// </summary>
        /// <param name="state">The system state</param>
        void OnDestroy(ref SystemState state);
    }

    /// <summary>
    /// Represents the system state passed to IStradaSystem methods.
    /// </summary>
    /// <remarks>
    /// This is a placeholder for Unity DOTS SystemState.
    /// In the actual implementation, this will be a using directive or wrapper
    /// around Unity.Entities.SystemState.
    /// </remarks>
    public struct SystemState
    {
        /// <summary>
        /// Gets the EntityManager for this system's world.
        /// </summary>
        public IEntityManager EntityManager { get; internal set; }

        /// <summary>
        /// Gets the current frame's delta time.
        /// </summary>
        public float DeltaTime { get; internal set; }

        /// <summary>
        /// Gets the total elapsed time since world creation.
        /// </summary>
        public double Time { get; internal set; }

        /// <summary>
        /// Gets whether this system is enabled.
        /// </summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Interface for EntityManager operations.
    /// </summary>
    /// <remarks>
    /// This wraps Unity DOTS EntityManager to keep systems testable.
    /// </remarks>
    public interface IEntityManager
    {
        /// <summary>
        /// Creates a new entity.
        /// </summary>
        /// <returns>The created entity handle</returns>
        Entity CreateEntity();

        /// <summary>
        /// Creates a new entity with the specified archetype.
        /// </summary>
        /// <param name="archetype">The archetype defining the entity's components</param>
        /// <returns>The created entity handle</returns>
        Entity CreateEntity(EntityArchetype archetype);

        /// <summary>
        /// Destroys an entity.
        /// </summary>
        /// <param name="entity">The entity to destroy</param>
        void DestroyEntity(Entity entity);

        /// <summary>
        /// Checks if an entity exists.
        /// </summary>
        /// <param name="entity">The entity to check</param>
        /// <returns>True if the entity exists</returns>
        bool Exists(Entity entity);

        /// <summary>
        /// Adds a component to an entity.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entity">The target entity</param>
        void AddComponent<T>(Entity entity) where T : struct, IStradaComponent;

        /// <summary>
        /// Removes a component from an entity.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entity">The target entity</param>
        void RemoveComponent<T>(Entity entity) where T : struct, IStradaComponent;

        /// <summary>
        /// Checks if an entity has a component.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entity">The entity to check</param>
        /// <returns>True if the entity has the component</returns>
        bool HasComponent<T>(Entity entity) where T : struct, IStradaComponent;

        /// <summary>
        /// Gets a component from an entity.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entity">The entity to get the component from</param>
        /// <returns>The component data</returns>
        T GetComponent<T>(Entity entity) where T : struct, IStradaComponent;

        /// <summary>
        /// Sets a component on an entity.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entity">The entity to set the component on</param>
        /// <param name="component">The component data</param>
        void SetComponent<T>(Entity entity, T component) where T : struct, IStradaComponent;
    }

    /// <summary>
    /// Represents an entity in the ECS world.
    /// </summary>
    /// <remarks>
    /// Entities are lightweight handles (just an ID and version).
    /// They represent game objects in the ECS simulation.
    /// </remarks>
    public struct Entity : IEquatable<Entity>
    {
        /// <summary>
        /// The entity's unique identifier.
        /// </summary>
        public int Index;

        /// <summary>
        /// The entity's version (for detecting reuse).
        /// </summary>
        public int Version;

        /// <summary>
        /// Gets a null entity (invalid).
        /// </summary>
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

    /// <summary>
    /// Represents an archetype - a unique combination of component types.
    /// </summary>
    /// <remarks>
    /// Entities with the same archetype are stored together in memory for cache efficiency.
    /// </remarks>
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
