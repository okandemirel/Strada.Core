using System;
using UnityEngine;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Serializable entry for referencing a ModuleConfig in GameBootstrapperConfig.
    /// Allows enabling/disabling modules without removing them from the list.
    /// </summary>
    [Serializable]
    public class ModuleEntry
    {
        [Tooltip("The module configuration asset")]
        [SerializeField] private ModuleConfig _config;

        [Tooltip("Whether this module is enabled")]
        [SerializeField] private bool _enabled = true;

        /// <summary>
        /// Gets the module configuration.
        /// </summary>
        public ModuleConfig Config => _config;

        /// <summary>
        /// Gets whether this module entry is enabled.
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Gets whether this entry has a valid config and the config is also enabled.
        /// </summary>
        public bool IsActiveAndValid => _config != null && _enabled && _config.Enabled;

        /// <summary>
        /// Gets the effective priority (from the config).
        /// </summary>
        public int Priority => _config?.Priority ?? int.MaxValue;

        /// <summary>
        /// Gets the module name for display.
        /// </summary>
        public string DisplayName => _config?.ModuleName ?? "(None)";

        public ModuleEntry() { }

        public ModuleEntry(ModuleConfig config, bool enabled = true)
        {
            _config = config;
            _enabled = enabled;
        }
    }
}
