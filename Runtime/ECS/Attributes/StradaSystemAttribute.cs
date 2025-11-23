using System;

namespace Strada.Core.ECS
{
    /// <summary>
    /// Marks a struct as a Strada ECS system for automatic registration.
    /// </summary>
    /// <remarks>
    /// Systems marked with this attribute are automatically discovered and registered
    /// during world initialization. The system will be added to the specified update group.
    ///
    /// Systems should implement IStradaSystem and be declared as partial structs
    /// for optimal performance.
    ///
    /// Example:
    /// <code>
    /// [StradaSystem(UpdateInGroup = typeof(SimulationSystemGroup))]
    /// [BurstCompile]
    /// public partial struct MovementSystem : IStradaSystem
    /// {
    ///     public void OnCreate(ref SystemState state) { }
    ///     public void OnUpdate(ref SystemState state)
    ///     {
    ///         // System logic here
    ///     }
    ///     public void OnDestroy(ref SystemState state) { }
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class StradaSystemAttribute : Attribute
    {
        /// <summary>
        /// The system group this system should update in.
        /// If null, the system updates in the default SimulationSystemGroup.
        /// </summary>
        public Type UpdateInGroup { get; set; }

        /// <summary>
        /// The priority order within the update group.
        /// Lower values execute first. Default is 0.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether this system is enabled by default.
        /// Default is true.
        /// </summary>
        public bool EnabledByDefault { get; set; }

        /// <summary>
        /// Systems that must execute before this system.
        /// </summary>
        public Type[] UpdateBefore { get; set; }

        /// <summary>
        /// Systems that must execute after this system.
        /// </summary>
        public Type[] UpdateAfter { get; set; }

        /// <summary>
        /// Initializes a new instance of StradaSystemAttribute.
        /// </summary>
        public StradaSystemAttribute()
        {
            Priority = 0;
            EnabledByDefault = true;
            UpdateBefore = Array.Empty<Type>();
            UpdateAfter = Array.Empty<Type>();
        }

        /// <summary>
        /// Validates the attribute configuration.
        /// </summary>
        /// <param name="systemType">The type this attribute is applied to</param>
        /// <exception cref="InvalidOperationException">Thrown if configuration is invalid</exception>
        public void Validate(Type systemType)
        {
            if (systemType == null)
                throw new ArgumentNullException(nameof(systemType));

            if (!systemType.IsValueType)
            {
                throw new InvalidOperationException(
                    $"StradaSystem attribute can only be applied to structs. {systemType.Name} is not a struct.");
            }

            if (!typeof(IStradaSystem).IsAssignableFrom(systemType))
            {
                throw new InvalidOperationException(
                    $"StradaSystem attribute requires type to implement IStradaSystem. {systemType.Name} does not implement IStradaSystem.");
            }

            if (UpdateInGroup != null && !typeof(ISystemGroup).IsAssignableFrom(UpdateInGroup))
            {
                throw new InvalidOperationException(
                    $"UpdateInGroup must be a system group type (implement ISystemGroup). {UpdateInGroup.Name} is not a system group.");
            }

            // Validate UpdateBefore systems
            if (UpdateBefore != null)
            {
                foreach (var type in UpdateBefore)
                {
                    if (!typeof(IStradaSystem).IsAssignableFrom(type))
                    {
                        throw new InvalidOperationException(
                            $"UpdateBefore type must implement IStradaSystem. {type.Name} does not.");
                    }
                }
            }

            // Validate UpdateAfter systems
            if (UpdateAfter != null)
            {
                foreach (var type in UpdateAfter)
                {
                    if (!typeof(IStradaSystem).IsAssignableFrom(type))
                    {
                        throw new InvalidOperationException(
                            $"UpdateAfter type must implement IStradaSystem. {type.Name} does not.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Marker interface for system groups.
    /// System groups organize related systems and control their update order.
    /// </summary>
    /// <remarks>
    /// Strada provides default system groups matching Unity DOTS:
    /// - InitializationSystemGroup: Runs at the start of each frame
    /// - SimulationSystemGroup: Main gameplay logic
    /// - PresentationSystemGroup: Rendering and presentation
    ///
    /// You can create custom system groups by implementing this interface.
    ///
    /// Example:
    /// <code>
    /// public struct PhysicsSystemGroup : ISystemGroup
    /// {
    ///     // Group contains physics-related systems
    /// }
    /// </code>
    /// </remarks>
    public interface ISystemGroup
    {
        // Marker interface - groups are identified by type
    }

    /// <summary>
    /// Default system group for initialization logic.
    /// Runs at the start of each frame, before SimulationSystemGroup.
    /// </summary>
    public struct InitializationSystemGroup : ISystemGroup
    {
    }

    /// <summary>
    /// Default system group for simulation logic.
    /// This is where most gameplay systems should run.
    /// </summary>
    public struct SimulationSystemGroup : ISystemGroup
    {
    }

    /// <summary>
    /// Default system group for presentation logic.
    /// Runs at the end of each frame, after SimulationSystemGroup.
    /// Used for rendering, UI updates, and other presentation tasks.
    /// </summary>
    public struct PresentationSystemGroup : ISystemGroup
    {
    }

    /// <summary>
    /// System group for fixed timestep updates (physics simulation).
    /// </summary>
    public struct FixedUpdateSystemGroup : ISystemGroup
    {
    }
}
