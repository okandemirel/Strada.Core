using System;
using Strada.Core.ECS.World;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Marks a class as a Strada ECS system that can be discovered and configured via the Inspector.
    /// Applied to SystemBase-derived classes to enable automatic discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class StradaSystemAttribute : Attribute
    {
        /// <summary>
        /// The module this system belongs to. Used for filtering during discovery.
        /// </summary>
        public string Module { get; set; }

        /// <summary>
        /// The category for organizing systems in the Inspector.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// A description shown as a tooltip in the Inspector.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The default update phase for this system.
        /// </summary>
        public UpdatePhase Phase { get; set; } = UpdatePhase.Update;

        /// <summary>
        /// The default execution order within the phase.
        /// </summary>
        public int Order { get; set; } = 0;

        public StradaSystemAttribute() { }

        public StradaSystemAttribute(string module)
        {
            Module = module;
        }
    }

    /// <summary>
    /// Specifies the default update phase for a system.
    /// Can be used independently or in combination with StradaSystemAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class UpdatePhaseAttribute : Attribute
    {
        public UpdatePhase Phase { get; }

        public UpdatePhaseAttribute(UpdatePhase phase)
        {
            Phase = phase;
        }
    }

    /// <summary>
    /// Specifies the default execution order for a system within its update phase.
    /// Lower values execute first.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ExecutionOrderAttribute : Attribute
    {
        public int Order { get; }

        public ExecutionOrderAttribute(int order)
        {
            Order = order;
        }
    }

    /// <summary>
    /// Specifies that a system runs before another system.
    /// Used for automatic ordering during discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RunBeforeAttribute : Attribute
    {
        public Type SystemType { get; }

        public RunBeforeAttribute(Type systemType)
        {
            SystemType = systemType ?? throw new ArgumentNullException(nameof(systemType));
        }
    }

    /// <summary>
    /// Specifies that a system runs after another system.
    /// Used for automatic ordering during discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RunAfterAttribute : Attribute
    {
        public Type SystemType { get; }

        public RunAfterAttribute(Type systemType)
        {
            SystemType = systemType ?? throw new ArgumentNullException(nameof(systemType));
        }
    }

    /// <summary>
    /// Marks a system as requiring another system to be present.
    /// Validation will fail if the required system is not enabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RequiresSystemAttribute : Attribute
    {
        public Type SystemType { get; }

        public RequiresSystemAttribute(Type systemType)
        {
            SystemType = systemType ?? throw new ArgumentNullException(nameof(systemType));
        }
    }

    /// <summary>
    /// Groups related systems together under a category.
    /// Used for organizing systems in the Inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SystemCategoryAttribute : Attribute
    {
        public string Category { get; }

        public SystemCategoryAttribute(string category)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
        }
    }

    /// <summary>
    /// Provides a description for a system shown as a tooltip in the Inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SystemDescriptionAttribute : Attribute
    {
        public string Description { get; }

        public SystemDescriptionAttribute(string description)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }
}
