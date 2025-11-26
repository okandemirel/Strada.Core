using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Strada.Core.DI;
using Strada.Core.Modules;
using Strada.Core;

namespace Strada.Core.Bootstrap
{
    /// <summary>
    /// Main entry point for initializing the Strada framework.
    /// Discovers modules, builds the DI container, and initializes all registered modules.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private BootstrapConfig _config;

        [Header("Runtime State")]
        [SerializeField] private bool _isInitialized;

        private ModuleRegistry _registry;
        private IContainer _container;
        private readonly List<IModuleInstaller> _initializedModules = new List<IModuleInstaller>();
        private Exception _initializationError;

        /// <summary>
        /// Gets the global DI container instance.
        /// </summary>
        public static IContainer Container { get; private set; }

        /// <summary>
        /// Gets whether the bootstrapper has completed initialization.
        /// </summary>
        public bool IsInitialized => _isInitialized;

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
            if (_config == null)
            {
                Debug.LogError("[GameBootstrapper] No BootstrapConfig assigned! Creating default config.");
                _config = BootstrapConfig.CreateDefault();
            }

            StradaPlayerLoop.Initialize();
            StartCoroutine(InitializeAsync());
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private IEnumerator InitializeAsync()
        {
            Exception error = null;

            Log("=== Strada Framework Bootstrap Started ===");

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

            _isInitialized = true;
            Container = _container;

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

            // Auto-binding: Register all [AutoRegister*] attributed types
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

        private void Shutdown()
        {
            if (!_isInitialized)
            {
                return;
            }

            Log("=== Strada Framework Shutdown Started ===");

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

            if (_container is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Container = null;
            _isInitialized = false;

            StradaPlayerLoop.Shutdown();

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
            if (_config.VerboseLogging)
            {
                Debug.Log($"[GameBootstrapper] {message}");
            }
        }

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
        /// Gets the module registry containing all discovered modules.
        /// </summary>
        public ModuleRegistry GetRegistry() => _registry;
    }
}
