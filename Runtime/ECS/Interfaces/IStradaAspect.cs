using System;

namespace Strada.Core.ECS
{
    /// <summary>
    /// Base interface for Strada ECS aspects.
    /// Aspects provide structured access to related components on an entity.
    /// </summary>
    /// <remarks>
    /// Strada aspects wrap Unity DOTS IAspect.
    /// They reduce boilerplate when systems need to access multiple components together.
    ///
    /// Aspects are readonly structs that provide a convenient API for accessing
    /// multiple components on the same entity.
    ///
    /// Best Practices:
    /// - Use readonly struct for aspects
    /// - Group logically related components
    /// - Provide convenient access methods
    /// - Keep aspects focused (3-5 components max)
    /// - Use RefRW for read-write access, RefRO for read-only
    ///
    /// Example:
    /// <code>
    /// public readonly struct BallAspect : IStradaAspect
    /// {
    ///     private readonly RefRW&lt;PositionComponent&gt; _position;
    ///     private readonly RefRW&lt;VelocityComponent&gt; _velocity;
    ///     private readonly RefRO&lt;PhysicsComponent&gt; _physics;
    ///
    ///     public float3 Position
    ///     {
    ///         get => _position.ValueRO.Value;
    ///         set => _position.ValueRW.Value = value;
    ///     }
    ///
    ///     public void ApplyGravity(float deltaTime)
    ///     {
    ///         var vel = _velocity.ValueRW;
    ///         vel.Value.y -= _physics.ValueRO.Gravity * deltaTime;
    ///         _velocity.ValueRW = vel;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public interface IStradaAspect
    {
        // Marker interface - aspects are defined by their component requirements
    }

    /// <summary>
    /// Read-write reference to a component.
    /// </summary>
    /// <typeparam name="T">The component type</typeparam>
    /// <remarks>
    /// RefRW provides both read and write access to a component.
    /// Use ValueRO for reading, ValueRW for writing.
    /// </remarks>
    public readonly struct RefRW<T> where T : struct, IStradaComponent
    {
        private readonly IntPtr _pointer;

        /// <summary>
        /// Gets the component value (read-only).
        /// </summary>
        public ref readonly T ValueRO => throw new NotImplementedException("RefRW is a placeholder");

        /// <summary>
        /// Gets the component value (read-write).
        /// </summary>
        public ref T ValueRW => throw new NotImplementedException("RefRW is a placeholder");

        /// <summary>
        /// Checks if this reference is valid.
        /// </summary>
        public bool IsValid => _pointer != IntPtr.Zero;
    }

    /// <summary>
    /// Read-only reference to a component.
    /// </summary>
    /// <typeparam name="T">The component type</typeparam>
    /// <remarks>
    /// RefRO provides read-only access to a component.
    /// This allows the job system to optimize parallel access.
    /// </remarks>
    public readonly struct RefRO<T> where T : struct, IStradaComponent
    {
        private readonly IntPtr _pointer;

        /// <summary>
        /// Gets the component value (read-only).
        /// </summary>
        public ref readonly T ValueRO => throw new NotImplementedException("RefRO is a placeholder");

        /// <summary>
        /// Checks if this reference is valid.
        /// </summary>
        public bool IsValid => _pointer != IntPtr.Zero;
    }

    /// <summary>
    /// Attribute to specify which components an aspect requires.
    /// </summary>
    /// <remarks>
    /// This attribute is used by the system generator to automatically
    /// create queries that match entities with the required components.
    ///
    /// Example:
    /// <code>
    /// [AspectComponents(typeof(PositionComponent), typeof(VelocityComponent))]
    /// public readonly struct MovementAspect : IStradaAspect
    /// {
    ///     // ...
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    public class AspectComponentsAttribute : Attribute
    {
        /// <summary>
        /// The component types this aspect requires.
        /// </summary>
        public Type[] ComponentTypes { get; }

        /// <summary>
        /// Initializes a new instance of AspectComponentsAttribute.
        /// </summary>
        /// <param name="componentTypes">The required component types</param>
        public AspectComponentsAttribute(params Type[] componentTypes)
        {
            ComponentTypes = componentTypes ?? Array.Empty<Type>();

            // Validate that all types implement IStradaComponent
            foreach (var type in ComponentTypes)
            {
                if (!typeof(IStradaComponent).IsAssignableFrom(type))
                {
                    throw new ArgumentException(
                        $"Type {type.Name} must implement IStradaComponent",
                        nameof(componentTypes));
                }
            }
        }
    }

    /// <summary>
    /// Specifies access mode for aspect components.
    /// </summary>
    public enum ComponentAccessMode
    {
        /// <summary>
        /// Read-only access (can be parallel).
        /// </summary>
        ReadOnly,

        /// <summary>
        /// Read-write access (exclusive).
        /// </summary>
        ReadWrite
    }

    /// <summary>
    /// Attribute to specify access mode for a component in an aspect.
    /// </summary>
    /// <remarks>
    /// Use this to control whether a component should be accessed read-only or read-write.
    /// Read-only access allows better parallelization.
    ///
    /// Example:
    /// <code>
    /// public readonly struct DamageAspect : IStradaAspect
    /// {
    ///     [ComponentAccess(ComponentAccessMode.ReadWrite)]
    ///     private readonly RefRW&lt;HealthComponent&gt; _health;
    ///
    ///     [ComponentAccess(ComponentAccessMode.ReadOnly)]
    ///     private readonly RefRO&lt;DefenseComponent&gt; _defense;
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ComponentAccessAttribute : Attribute
    {
        /// <summary>
        /// The access mode for this component.
        /// </summary>
        public ComponentAccessMode AccessMode { get; }

        /// <summary>
        /// Initializes a new instance of ComponentAccessAttribute.
        /// </summary>
        /// <param name="accessMode">The access mode</param>
        public ComponentAccessAttribute(ComponentAccessMode accessMode)
        {
            AccessMode = accessMode;
        }
    }
}
