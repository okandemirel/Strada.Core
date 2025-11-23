using System;

namespace Strada.Core.ECS
{
    /// <summary>
    /// Marker interface for Strada ECS components.
    /// Components are pure data structures with no logic.
    /// </summary>
    /// <remarks>
    /// Strada components wrap Unity DOTS IComponentData.
    /// They should be structs containing only data fields.
    ///
    /// Best Practices:
    /// - Use structs for components (value types)
    /// - Keep components small for cache efficiency
    /// - No methods or logic in components
    /// - Use Burst-compatible types only
    /// - Prefer blittable types when possible
    ///
    /// Example:
    /// <code>
    /// public struct VelocityComponent : IStradaComponent
    /// {
    ///     public float3 Value;
    ///     public float MaxSpeed;
    /// }
    /// </code>
    /// </remarks>
    public interface IStradaComponent
    {
        // Marker interface - no members required
    }

    /// <summary>
    /// Marker interface for component data that can be baked from ScriptableObjects.
    /// </summary>
    /// <remarks>
    /// Components implementing this interface can be automatically created
    /// during the baking process from ScriptableObject configuration.
    ///
    /// Example:
    /// <code>
    /// public struct BallPhysicsComponent : IStradaComponent, IBakeable
    /// {
    ///     public float Mass;
    ///     public float Bounciness;
    ///     public float Drag;
    /// }
    /// </code>
    /// </remarks>
    public interface IBakeable
    {
        // Marker interface - identifies components that can be baked
    }

    /// <summary>
    /// Marker interface for shared components.
    /// Shared components are the same across multiple entities.
    /// </summary>
    /// <remarks>
    /// Use shared components for data that is identical across many entities.
    /// This improves memory usage and cache performance.
    ///
    /// Warning: Changing a shared component value affects ALL entities sharing it.
    ///
    /// Example:
    /// <code>
    /// public struct RenderMeshComponent : IStradaComponent, ISharedComponent
    /// {
    ///     public Mesh MeshData;
    ///     public Material MaterialData;
    /// }
    /// </code>
    /// </remarks>
    public interface ISharedComponent : IStradaComponent
    {
        // Marker interface - identifies shared components
    }

    /// <summary>
    /// Marker interface for cleanup components.
    /// Cleanup components are automatically removed when their referenced entity is destroyed.
    /// </summary>
    /// <remarks>
    /// Use cleanup components to track references between entities.
    /// When the target entity is destroyed, the cleanup component is automatically removed.
    ///
    /// Example:
    /// <code>
    /// public struct TargetComponent : IStradaComponent, ICleanupComponent
    /// {
    ///     public Entity TargetEntity;
    /// }
    /// </code>
    /// </remarks>
    public interface ICleanupComponent : IStradaComponent
    {
        // Marker interface - identifies cleanup components
    }

    /// <summary>
    /// Marker interface for buffer components.
    /// Buffer components can store variable-length arrays of data.
    /// </summary>
    /// <remarks>
    /// Use buffer components when you need a dynamic array attached to an entity.
    /// Buffers are stored inline with the entity when small, external when large.
    ///
    /// Example:
    /// <code>
    /// public struct PathPointBuffer : IStradaComponent, IBufferComponent
    /// {
    ///     public float3 Position;
    /// }
    /// </code>
    /// </remarks>
    public interface IBufferComponent : IStradaComponent
    {
        // Marker interface - identifies buffer components
    }
}
