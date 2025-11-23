using System;
using UnityEngine;

namespace Strada.Core.ECS.Baking
{
    /// <summary>
    /// Configuration for the Strada baking system.
    /// </summary>
    /// <remarks>
    /// This ScriptableObject configures how baking operates:
    /// - Which assemblies to scan for bakers
    /// - Logging and debugging options
    /// - Performance settings
    /// - Validation options
    ///
    /// Create via: Assets → Create → Strada → Baking Config
    /// </remarks>
    [CreateAssetMenu(fileName = "BakingConfig", menuName = "Strada/Baking Config", order = 100)]
    public class BakingConfig : ScriptableObject
    {
        [Header("Baker Discovery")]
        [Tooltip("Automatically discover bakers in the project")]
        [SerializeField]
        private bool _autoDiscoverBakers = true;

        [Tooltip("Assembly name patterns to include (e.g., 'Strada.*', 'Game.*')")]
        [SerializeField]
        private string[] _assemblyIncludePatterns = new[] { "Strada.*", "Game.*", "*.Game" };

        [Tooltip("Assembly name patterns to exclude")]
        [SerializeField]
        private string[] _assemblyExcludePatterns = new[]
        {
            "Unity.*",
            "UnityEngine.*",
            "UnityEditor.*",
            "System.*",
            "mscorlib",
            "netstandard",
            "*.Tests"
        };

        [Header("Validation")]
        [Tooltip("Validate authoring data before baking")]
        [SerializeField]
        private bool _validateBeforeBaking = true;

        [Tooltip("Fail baking on validation errors")]
        [SerializeField]
        private bool _failOnValidationError = true;

        [Tooltip("Validate all ScriptableObject configs on project load")]
        [SerializeField]
        private bool _validateOnProjectLoad = false;

        [Header("Logging")]
        [Tooltip("Enable verbose baking logs")]
        [SerializeField]
        private bool _verboseLogging = false;

        [Tooltip("Log successful baking operations")]
        [SerializeField]
        private bool _logSuccessfulBakes = false;

        [Tooltip("Log baking warnings")]
        [SerializeField]
        private bool _logWarnings = true;

        [Tooltip("Log baking errors")]
        [SerializeField]
        private bool _logErrors = true;

        [Header("Performance")]
        [Tooltip("Maximum number of bakers to process per frame")]
        [SerializeField]
        private int _maxBakersPerFrame = 100;

        [Tooltip("Enable incremental baking (only rebake changed assets)")]
        [SerializeField]
        private bool _incrementalBaking = true;

        [Header("Debug")]
        [Tooltip("Enable baking debug mode (extra validation and logging)")]
        [SerializeField]
        private bool _debugMode = false;

        /// <summary>
        /// Gets whether to automatically discover bakers.
        /// </summary>
        public bool AutoDiscoverBakers => _autoDiscoverBakers;

        /// <summary>
        /// Gets the assembly include patterns.
        /// </summary>
        public string[] AssemblyIncludePatterns => _assemblyIncludePatterns;

        /// <summary>
        /// Gets the assembly exclude patterns.
        /// </summary>
        public string[] AssemblyExcludePatterns => _assemblyExcludePatterns;

        /// <summary>
        /// Gets whether to validate before baking.
        /// </summary>
        public bool ValidateBeforeBaking => _validateBeforeBaking;

        /// <summary>
        /// Gets whether to fail on validation errors.
        /// </summary>
        public bool FailOnValidationError => _failOnValidationError;

        /// <summary>
        /// Gets whether to validate on project load.
        /// </summary>
        public bool ValidateOnProjectLoad => _validateOnProjectLoad;

        /// <summary>
        /// Gets whether verbose logging is enabled.
        /// </summary>
        public bool VerboseLogging => _verboseLogging;

        /// <summary>
        /// Gets whether to log successful bakes.
        /// </summary>
        public bool LogSuccessfulBakes => _logSuccessfulBakes;

        /// <summary>
        /// Gets whether to log warnings.
        /// </summary>
        public bool LogWarnings => _logWarnings;

        /// <summary>
        /// Gets whether to log errors.
        /// </summary>
        public bool LogErrors => _logErrors;

        /// <summary>
        /// Gets the maximum number of bakers to process per frame.
        /// </summary>
        public int MaxBakersPerFrame => _maxBakersPerFrame;

        /// <summary>
        /// Gets whether incremental baking is enabled.
        /// </summary>
        public bool IncrementalBaking => _incrementalBaking;

        /// <summary>
        /// Gets whether debug mode is enabled.
        /// </summary>
        public bool DebugMode => _debugMode;

        /// <summary>
        /// Validates this configuration.
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if configuration is valid</returns>
        public bool Validate(out string errorMessage)
        {
            if (_maxBakersPerFrame <= 0)
            {
                errorMessage = "MaxBakersPerFrame must be greater than 0";
                return false;
            }

            if (_assemblyIncludePatterns == null || _assemblyIncludePatterns.Length == 0)
            {
                errorMessage = "AssemblyIncludePatterns cannot be empty";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Checks if an assembly name matches the include/exclude patterns.
        /// </summary>
        /// <param name="assemblyName">The assembly name to check</param>
        /// <returns>True if the assembly should be scanned</returns>
        public bool ShouldScanAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            // Check exclude patterns first
            foreach (var excludePattern in _assemblyExcludePatterns)
            {
                if (MatchesPattern(assemblyName, excludePattern))
                    return false;
            }

            // Check include patterns
            foreach (var includePattern in _assemblyIncludePatterns)
            {
                if (MatchesPattern(assemblyName, includePattern))
                    return true;
            }

            return false;
        }

        private bool MatchesPattern(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            // Simple wildcard matching
            if (pattern == "*")
                return true;

            if (pattern.EndsWith("*") && pattern.StartsWith("*"))
            {
                var inner = pattern.Substring(1, pattern.Length - 2);
                return text.Contains(inner);
            }

            if (pattern.StartsWith("*"))
            {
                var suffix = pattern.Substring(1);
                return text.EndsWith(suffix);
            }

            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return text.StartsWith(prefix);
            }

            return text == pattern;
        }

        /// <summary>
        /// Creates a default baking configuration.
        /// </summary>
        /// <returns>A new baking config with default values</returns>
        public static BakingConfig CreateDefault()
        {
            var config = CreateInstance<BakingConfig>();
            config._autoDiscoverBakers = true;
            config._validateBeforeBaking = true;
            config._failOnValidationError = true;
            config._verboseLogging = false;
            config._logWarnings = true;
            config._logErrors = true;
            config._maxBakersPerFrame = 100;
            config._incrementalBaking = true;
            config._debugMode = false;
            return config;
        }

        private void OnValidate()
        {
            // Clamp values
            if (_maxBakersPerFrame < 1)
                _maxBakersPerFrame = 1;

            // Validate configuration
            if (!Validate(out var errorMessage))
            {
                Debug.LogError($"BakingConfig validation failed: {errorMessage}", this);
            }
        }

        private void Reset()
        {
            // Reset to defaults when created
            var defaultConfig = CreateDefault();
            _autoDiscoverBakers = defaultConfig._autoDiscoverBakers;
            _assemblyIncludePatterns = defaultConfig._assemblyIncludePatterns;
            _assemblyExcludePatterns = defaultConfig._assemblyExcludePatterns;
            _validateBeforeBaking = defaultConfig._validateBeforeBaking;
            _failOnValidationError = defaultConfig._failOnValidationError;
            _validateOnProjectLoad = defaultConfig._validateOnProjectLoad;
            _verboseLogging = defaultConfig._verboseLogging;
            _logSuccessfulBakes = defaultConfig._logSuccessfulBakes;
            _logWarnings = defaultConfig._logWarnings;
            _logErrors = defaultConfig._logErrors;
            _maxBakersPerFrame = defaultConfig._maxBakersPerFrame;
            _incrementalBaking = defaultConfig._incrementalBaking;
            _debugMode = defaultConfig._debugMode;
        }
    }
}
