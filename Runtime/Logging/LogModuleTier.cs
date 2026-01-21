namespace Strada.Core.Logging
{
    /// <summary>
    /// Defines the three tiers of log modules with different visibility behaviors.
    /// </summary>
    public enum LogModuleTier
    {
        /// <summary>
        /// Strada Core modules (IDs 0-99). Framework internals like DI, ECS, Sync, etc.
        /// Visibility is locked, colors are editable.
        /// </summary>
        StradaCore = 0,

        /// <summary>
        /// Strada Module tier (IDs 100-999). Strada framework modules like Screen, etc.
        /// Visibility is locked, colors are editable.
        /// </summary>
        StradaModule = 1,

        /// <summary>
        /// Game tier (IDs 1000+). Project-specific modules.
        /// Both visibility and colors are editable.
        /// </summary>
        Game = 2
    }
}
