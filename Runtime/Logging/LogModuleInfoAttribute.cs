using System;

namespace Strada.Core.Logging
{
    /// <summary>
    /// Attribute to define metadata for LogModule enum values.
    /// Specifies tier, default visibility, and optional display name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class LogModuleInfoAttribute : Attribute
    {
        /// <summary>
        /// The tier this module belongs to.
        /// </summary>
        public LogModuleTier Tier { get; }

        /// <summary>
        /// Default visibility state for this module.
        /// </summary>
        public bool DefaultVisible { get; }

        /// <summary>
        /// Optional human-readable display name. If null, uses enum name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Creates a new LogModuleInfo attribute.
        /// </summary>
        /// <param name="tier">The tier this module belongs to.</param>
        /// <param name="defaultVisible">Default visibility state (defaults to true).</param>
        /// <param name="displayName">Optional display name (defaults to null, using enum name).</param>
        public LogModuleInfoAttribute(LogModuleTier tier, bool defaultVisible = true, string displayName = null)
        {
            Tier = tier;
            DefaultVisible = defaultVisible;
            DisplayName = displayName;
        }
    }
}
