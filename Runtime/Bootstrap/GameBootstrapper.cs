using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Strada.Core.DI;
using Strada.Core.Modules;
using Strada.Core;
using Strada.Core.Core;
using Strada.Core.Communication;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.World;

namespace Strada.Core.Bootstrap
{
    /// <summary>
    /// Main entry point for initializing the Strada framework.
    /// Supports two modes:
    /// 1. New Mode (Recommended): Use GameBootstrapperConfig with ModuleConfig ScriptableObjects
    /// 2. Legacy Mode: Use BootstrapConfig with IModuleInstaller classes
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Configuration (Use ONE)")]
        [Tooltip("New unified config approach (recommended)")]
        [SerializeField] private GameBootstrapperConfig _gameConfig;

        [Tooltip("Legacy config (deprecated - use GameBootstrapperConfig instead)")]
        [SerializeField] private BootstrapConfig _config;

        [Header("Runtime State")]
        [SerializeField] private bool _isInitialized;

        // Legacy support
        private ModuleRegistry _registry;
        private readonly List<IModuleInstaller> _initializedModules = new List<IModuleInstaller>();

        // New config support
        private readonly List<ModuleConfig> _initializedModuleConfigs = new List<ModuleConfig>();
        private SystemRunner _systemRunner;
        private ECS.World.World _world;

        // Shared
        private IContainer _container;
        private IServiceLocator _serviceLocator;
        private Exception _initializationError;

        /// <summary>
        /// Gets the global DI container instance.
        /// </summary>
        public static IContainer Container { get; private set; }

        /// <summary>
        /// Gets the global service locator instance.
        /// </summary>
        public static IServiceLocator Services { get; private set; }

        /// <summary>
        /// Gets the ECS World instance.
        /// </summary>
        public static ECS.World.World World { get; private set; }

        /// <summary>
        /// Gets whether the bootstrapper has completed initialization.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets whether using the new GameBootstrapperConfig.
        /// </summary>
        public bool UsingNewConfig => _gameConfig != null;

        /// <summary>
        /// Event raised when initialization completes successfully.
        /// </summary>
        public event Action OnInitializationComplete;

        /// <summary>
        /// Event raised when initialization fails.
        /// </summary>
        public event Action<Exception> OnInitializationFailed;

        private void Awake()
        {
            // Prefer new config
            if (_gameConfig != null)
            {
                Log("Using GameBootstrapperConfig (new unified approach)");
            }
            else if (_config != null)
            {
                Log("Using BootstrapConfig (legacy approach - consider migrating to GameBootstrapperConfig)");
            }
            else
            {
                Debug.LogError("[GameBootstrapper] No configuration assigned! Please assign a GameBootstrapperConfig.");
                _config = BootstrapConfig.CreateDefault();
            }

            PlayerLoop.Initialize();
            StartCoroutine(InitializeAsync());
        }

        private void Update()
        {
            if (!_isInitialized) return;
            _systemRunner?.Update(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!_isInitialized) return;
            _systemRunner?.LateUpdate(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (!_isInitialized) return;
            _systemRunner?.FixedUpdate(Time.fixedDeltaTime);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private IEnumerator InitializeAsync()
        {
            Log("=== Strada Framework Bootstrap Started ===");

            if (_gameConfig != null)
            {
                yield return StartCoroutine(InitializeWithNewConfigAsync());
            }
            else
            {
                yield return StartCoroutine(InitializeWithLegacyConfigAsync());
            }
        }

        #region New Config Initialization

        private IEnumerator InitializeWithNewConfigAsync()
        {
            Exception error = null;

            // Phase 1: Validation
            Log("Phase 1: Configuration Validation");
            if (_gameConfig.ValidateOnStart)
            {
                if (!_gameConfig.Validate(out var errors))
                {
                    var errorMessage = string.Join("\n", errors);
                    if (_gameConfig.FailOnValidationError)
                    {
                        HandleInitializationError(new InvalidOperationException($"Validation failed:\n{errorMessage}"));
                        yield break;
                    }
                    else
                    {
                        Debug.LogWarning($"[GameBootstrapper] Validation warnings:\n{errorMessage}");
                    }
                }
                else
                {
                    Log("Validation passed");
                }
            }

            // Phase 2: Build Container
            Log("Phase 2: Building Container");
            if (!TryExecute(() => BuildContainerFromGameConfig(), "Container Building", out error))
            {
                HandleInitializationError(error);
                yield break;
            }

            // Phase 3: Create World and SystemRunner
            Log("Phase 3: Creating ECS World");
            if (!TryExecute(() => CreateWorld(), "World Creation", out error))
            {
                HandleInitializationError(error);
                yield break;
            }

            // Phase 4: Initialize Modules
            Log("Phase 4: Module Initialization");
            yield return StartCoroutine(InitializeModuleConfigsAsync());

            if (_initializationError != null)
            {
                HandleInitializationError(_initializationError);
                yield break;
            }

            // Phase 5: Initialize Systems
            Log("Phase 5: System Initialization");
            if (!TryExecute(() => InitializeSystems(), "System Initialization", out error))
            {
                HandleInitializationError(error);
                yield break;
            }

            CompleteInitialization();
        }

        private void BuildContainerFromGameConfig()
        {
            var builder = new ContainerBuilder();
            var moduleBuilder = new ModuleBuilder(builder);

            // Install services from each module
            foreach (var module in _gameConfig.GetEnabledModules())
            {
                Log($"Installing module: {module.ModuleName}");
                module.Install(moduleBuilder);
            }

            _container = builder.Build();
            _serviceLocator = new ServiceLocator(_container);

            Log($"Container built with {_gameConfig.EnabledModuleCount} modules");
        }

        private void CreateWorld()
        {
            _world = new WorldBuilder().Build();
            ECS.World.World.Current = _world;

            _systemRunner = new SystemRunner(_world.EntityManager, _world.MessageBus, _container);
            _systemRunner.AddSystemsFromConfigs(_gameConfig.GetEnabledModules());

            Log($"World created with {_systemRunner.SystemCount} systems");
        }

        private IEnumerator InitializeModuleConfigsAsync()
        {
            _initializationError = null;

            foreach (var module in _gameConfig.GetEnabledModules())
            {
                try
                {
                    Log($"Initializing module: {module.ModuleName}");
                    module.Initialize(_serviceLocator);
                    _initializedModuleConfigs.Add(module);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameBootstrapper] Failed to initialize module {module.ModuleName}: {ex.Message}");
                    _initializationError = ex;
                    yield break;
                }

                if (_gameConfig.AsyncInitialization)
                {
                    yield return null;
                }
            }

            Log($"Initialized {_initializedModuleConfigs.Count} modules");
        }

        private void InitializeSystems()
        {
            _world.Initialize();
            _systemRunner.Initialize();
            Log("Systems initialized");
        }

        #endregion

        #region Legacy Config Initialization

        private IEnumerator InitializeWithLegacyConfigAsync()
        {
            Exception error = null;

            Log("Phase 1: Module Discovery");
            yield return StartCoroutine(DiscoverModulesAsync());

            if (!TryExecute(() => ValidateDependencies(), "Dependency Validation", out error))
            {
                HandleInitializationError(error);
                yield break;
            }

            Log("Phase 3: Container Building");
            if (!TryExecute(() => BuildContainer(), "Container Building", out error))
            {
                HandleInitializationError(error);
                yield break;
            }

            Log("Phase 4: Module Initialization");
            yield return StartCoroutine(InitializeModulesAsync());

            if (_initializationError != null)
            {
                HandleInitializationError(_initializationError);
                yield break;
            }

            CompleteInitialization();
        }

        private IEnumerator DiscoverModulesAsync()
        {
            _registry = new ModuleRegistry();

            if (_config.AutoDiscoverModules)
            {
                Log("Auto-discovering modules...");

                _registry.DiscoverModules(assembly =>
                {
                    var assemblyName = assembly.GetName().Name;

                    foreach (var excludePattern in _config.AssemblyExcludePatterns)
                    {
                        if (MatchesPattern(assemblyName, excludePattern))
                        {
                            return false;
                        }
                    }

                    foreach (var includePattern in _config.AssemblyIncludePatterns)
                    {
                        if (MatchesPattern(assemblyName, includePattern))
                        {
                            return true;
                        }
                    }

                    return false;
                });

                Log($"Discovered {_registry.Modules.Count} modules");
            }
            else if (_config.ManualModules.Count > 0)
            {
                Log("Using manually configured modules...");

                foreach (var moduleRef in _config.ManualModules.Where(m => m.Enabled))
                {
                    try
                    {
                        var type = Type.GetType(moduleRef.TypeName);
                        if (type == null)
                        {
                            Debug.LogWarning($"[GameBootstrapper] Module type not found: {moduleRef.TypeName}");
                            continue;
                        }

                        var installer = (IModuleInstaller)Activator.CreateInstance(type);
                        _registry.RegisterModule(installer, moduleRef.Priority);
                        Log($"Registered manual module: {type.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GameBootstrapper] Failed to create module {moduleRef.TypeName}: {ex.Message}");
                    }
                }

                Log($"Registered {_registry.Modules.Count} manual modules");
            }
            else
            {
                Debug.LogWarning("[GameBootstrapper] No modules discovered or configured!");
            }

            foreach (var module in _registry.Modules)
            {
                Log($"  - {module.Name} (Priority: {module.Priority})");
            }

            yield return null;
        }

        private void ValidateDependencies()
        {
            if (!_config.ValidateDependencies)
            {
                Log("Dependency validation skipped");
                return;
            }

            Log("Validating module dependencies...");

            if (!_registry.Validate(out var errorMessage))
            {
                var error = $"Module dependency validation failed: {errorMessage}";

                if (_config.FailOnValidationError)
                {
                    throw new InvalidOperationException(error);
                }
                else
                {
                    Debug.LogWarning($"[GameBootstrapper] {error}");
                }
            }
            else
            {
                Log("Dependency validation passed");
            }
        }

        private void BuildContainer()
        {
            Log("Building DI container...");

            var builder = new ContainerBuilder();

            if (_config.EnableAutoBinding)
            {
                Log("Registering auto-bindings...");
                builder.RegisterAutoBindings(
                    _config.AssemblyIncludePatterns,
                    _config.AssemblyExcludePatterns,
                    _config.ForceRuntimeScanning);
                Log($"Auto-binding registered {ContainerBuilderExtensions.GetAutoBindingCount()} services");
            }

            foreach (var module in _registry.Modules)
            {
                try
                {
                    Log($"Installing module: {module.Name}");
                    module.Installer.Install(builder);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameBootstrapper] Failed to install module {module.Name}: {ex.Message}");
                    throw;
                }
            }

            _container = builder.Build();
            _serviceLocator = new ServiceLocator(_container);
            Log($"Container built successfully");
        }

        private IEnumerator InitializeModulesAsync()
        {
            Log("Initializing modules...");
            _initializationError = null;

            foreach (var module in _registry.Modules)
            {
                try
                {
                    Log($"Initializing module: {module.Name}");
                    module.Installer.Initialize(_container);
                    _initializedModules.Add(module.Installer);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameBootstrapper] Failed to initialize module {module.Name}: {ex.Message}");
                    _initializationError = ex;
                    yield break;
                }

                if (_config.AsyncInitialization)
                {
                    yield return null;
                }
            }

            Log($"Initialized {_initializedModules.Count} modules");
        }

        #endregion

        #region Shared Methods

        private void CompleteInitialization()
        {
            _isInitialized = true;
            Container = _container;
            Services = _serviceLocator;
            World = _world;

            Log("=== Strada Framework Bootstrap Complete ===");
            OnInitializationComplete?.Invoke();
        }

        private bool TryExecute(Action action, string phaseName, out Exception error)
        {
            error = null;
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                Debug.LogError($"[GameBootstrapper] {phaseName} failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private void HandleInitializationError(Exception ex)
        {
            Debug.LogError($"[GameBootstrapper] Initialization failed: {ex.Message}\n{ex.StackTrace}");
            OnInitializationFailed?.Invoke(ex);
            _isInitialized = false;
        }

        private void Shutdown()
        {
            if (!_isInitialized)
            {
                return;
            }

            Log("=== Strada Framework Shutdown Started ===");

            // Shutdown new config modules (in reverse order)
            for (int i = _initializedModuleConfigs.Count - 1; i >= 0; i--)
            {
                try
                {
                    var module = _initializedModuleConfigs[i];
                    Log($"Shutting down module: {module.ModuleName}");
                    module.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameBootstrapper] Error during module shutdown: {ex.Message}");
                }
            }
            _initializedModuleConfigs.Clear();

            // Shutdown legacy modules (in reverse order)
            for (int i = _initializedModules.Count - 1; i >= 0; i--)
            {
                try
                {
                    var module = _initializedModules[i];
                    Log($"Shutting down module: {module.GetType().Name}");
                    module.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameBootstrapper] Error during module shutdown: {ex.Message}");
                }
            }
            _initializedModules.Clear();

            // Dispose SystemRunner
            _systemRunner?.Dispose();
            _systemRunner = null;

            // Dispose World
            _world?.Dispose();
            _world = null;
            ECS.World.World.Current = null;

            // Dispose Container
            if (_container is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Container = null;
            Services = null;
            World = null;
            _isInitialized = false;

            PlayerLoop.Shutdown();

            Log("=== Strada Framework Shutdown Complete ===");
        }

        private bool MatchesPattern(string value, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(value, regexPattern);
        }

        private void Log(string message)
        {
            bool verbose = _gameConfig != null ? _gameConfig.VerboseLogging :
                          (_config != null && _config.VerboseLogging);
            if (verbose)
            {
                Debug.Log($"[GameBootstrapper] {message}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually triggers initialization. Only use if auto-initialization is disabled.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[GameBootstrapper] Already initialized!");
                return;
            }

            StartCoroutine(InitializeAsync());
        }

        /// <summary>
        /// Gets the module registry containing all discovered modules (legacy mode only).
        /// </summary>
        public ModuleRegistry GetRegistry() => _registry;

        /// <summary>
        /// Gets the SystemRunner instance (new config mode only).
        /// </summary>
        public SystemRunner GetSystemRunner() => _systemRunner;

        /// <summary>
        /// Gets debug information about the current state.
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== GameBootstrapper Debug Info ===");
            sb.AppendLine($"Initialized: {_isInitialized}");
            sb.AppendLine($"Using New Config: {UsingNewConfig}");

            if (_gameConfig != null)
            {
                sb.AppendLine($"Enabled Modules: {_gameConfig.EnabledModuleCount}");
            }

            if (_systemRunner != null)
            {
                sb.AppendLine();
                sb.Append(_systemRunner.GetDebugInfo());
            }

            return sb.ToString();
        }

        #endregion
    }
}
