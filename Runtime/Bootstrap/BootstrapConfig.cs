using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strada.Core.Bootstrap
{
    /// <summary>
    /// ScriptableObject configuration for the Strada framework bootstrap process.
    /// Controls which modules are loaded and in what order.
    /// </summary>
    /// <remarks>
    /// DEPRECATED: Use GameBootstrapperConfig with ModuleConfig ScriptableObjects instead.
    /// This provides a more unified, VContainer-like experience with Inspector-based configuration.
    /// </remarks>
    [Obsolete("Use GameBootstrapperConfig with ModuleConfig ScriptableObjects instead.")]
    [CreateAssetMenu(fileName = "BootstrapConfig", menuName = "Strada/Legacy/Bootstrap Config (Deprecated)", order = 100)]
    public class BootstrapConfig : ScriptableObject
    {
        [Header("Module Discovery")]
        [Tooltip("Enable automatic module discovery via reflection")]
        [SerializeField] private bool _autoDiscoverModules = true;

        [Tooltip("Assembly name patterns to include in discovery (e.g., 'Strada.*', 'Game.*')")]
        [SerializeField] private List<string> _assemblyIncludePatterns = new List<string> { "Strada.*", "Game.*" };

        [Tooltip("Assembly name patterns to exclude from discovery (e.g., 'Unity.*', 'System.*')")]
        [SerializeField] private List<string> _assemblyExcludePatterns = new List<string> { "Unity.*", "System.*", "Mono.*", "mscorlib", "*.Tests", "*.Tests.*", "*Test*" };

        [Header("Manual Module Configuration")]
        [Tooltip("Manually specify module types to load (overrides auto-discovery if set)")]
        [SerializeField] private List<ModuleReference> _manualModules = new List<ModuleReference>();

        [Header("Debug Options")]
        [Tooltip("Log detailed information during bootstrap")]
        [SerializeField] private bool _verboseLogging = true;

        [Tooltip("Validate module dependencies before initialization")]
        [SerializeField] private bool _validateDependencies = true;

        [Tooltip("Stop initialization if validation fails")]
        [SerializeField] private bool _failOnValidationError = true;

        [Header("Performance")]
        [Tooltip("Initialize modules asynchronously (experimental)")]
        [SerializeField] private bool _asyncInitialization = false;

        [Header("Auto-Binding")]
        [Tooltip("Enable auto-binding for [AutoRegister] attributes")]
        [SerializeField] private bool _enableAutoBinding = true;

        [Tooltip("Force runtime reflection scanning (disable source generation)")]
        [SerializeField] private bool _forceRuntimeScanning = false;

        /// <summary>
        /// Gets whether automatic module discovery is enabled.
        /// </summary>
        public bool AutoDiscoverModules => _autoDiscoverModules;

        /// <summary>
        /// Gets the assembly include patterns for module discovery.
        /// </summary>
        public IReadOnlyList<string> AssemblyIncludePatterns => _assemblyIncludePatterns;

        /// <summary>
        /// Gets the assembly exclude patterns for module discovery.
        /// </summary>
        public IReadOnlyList<string> AssemblyExcludePatterns => _assemblyExcludePatterns;

        /// <summary>
        /// Gets the manually configured modules.
        /// </summary>
        public IReadOnlyList<ModuleReference> ManualModules => _manualModules;

        /// <summary>
        /// Gets whether verbose logging is enabled.
        /// </summary>
        public bool VerboseLogging => _verboseLogging;

        /// <summary>
        /// Gets whether dependency validation is enabled.
        /// </summary>
        public bool ValidateDependencies => _validateDependencies;

        /// <summary>
        /// Gets whether to fail on validation errors.
        /// </summary>
        public bool FailOnValidationError => _failOnValidationError;

        /// <summary>
        /// Gets whether asynchronous initialization is enabled.
        /// </summary>
        public bool AsyncInitialization => _asyncInitialization;

        /// <summary>
        /// Gets whether auto-binding is enabled.
        /// </summary>
        public bool EnableAutoBinding => _enableAutoBinding;

        /// <summary>
        /// Gets whether to force runtime scanning over source generation.
        /// </summary>
        public bool ForceRuntimeScanning => _forceRuntimeScanning;

        /// <summary>
        /// Creates a default bootstrap configuration.
        /// </summary>
        public static BootstrapConfig CreateDefault()
        {
            var config = CreateInstance<BootstrapConfig>();
            config._autoDiscoverModules = true;
            config._verboseLogging = true;
            config._validateDependencies = true;
            config._failOnValidationError = true;
            config._asyncInitialization = false;
            config._enableAutoBinding = true;
            config._forceRuntimeScanning = false;
            config._assemblyIncludePatterns = new List<string> { "Strada.*", "Game.*" };
            config._assemblyExcludePatterns = new List<string> { "Unity.*", "System.*", "Mono.*", "mscorlib", "*.Tests", "*.Tests.*", "*Test*" };
            return config;
        }

        private void OnValidate()
        {
            if (_assemblyIncludePatterns.Count == 0)
            {
                Debug.LogWarning("[BootstrapConfig] No assembly include patterns specified. Module discovery may not find any modules.");
            }
        }
    }

    /// <summary>
    /// Reference to a module installer for manual configuration.
    /// </summary>
    [Serializable]
    public class ModuleReference
    {
        [Tooltip("The fully qualified type name of the module installer")]
        [SerializeField] private string _typeName;

        [Tooltip("Initialization priority (lower values initialize first)")]
        [SerializeField] private int _priority;

        [Tooltip("Whether this module is enabled")]
        [SerializeField] private bool _enabled = true;

        /// <summary>
        /// Gets or sets the fully qualified type name of the module installer.
        /// </summary>
        public string TypeName
        {
            get => _typeName;
            set => _typeName = value;
        }

        /// <summary>
        /// Gets or sets the initialization priority.
        /// </summary>
        public int Priority
        {
            get => _priority;
            set => _priority = value;
        }

        /// <summary>
        /// Gets or sets whether this module is enabled.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }
    }
}
