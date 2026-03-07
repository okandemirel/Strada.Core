using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Strada.Core.DI;
using Strada.Core.Modules;
using Strada.Core.Core;
using Strada.Core.Communication;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.World;
using Strada.Core.Sync;

namespace Strada.Core.Bootstrap
{
    /// <summary>
    /// Main entry point for initializing the Strada framework.
    /// Uses GameBootstrapperConfig with ModuleConfig ScriptableObjects.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrapper : MonoBehaviour
    {
        private static GameBootstrapper _instance;

        [Header("Configuration")]
        [Tooltip("Game bootstrapper configuration")]
        [SerializeField] private GameBootstrapperConfig _gameConfig;

        [Header("Runtime State")]
        [SerializeField] private bool _isInitialized;

        private readonly List<ModuleConfig> _initializedModuleConfigs = new List<ModuleConfig>();
        private SystemRunner _systemRunner;
        private ECS.World.World _world;
        private EventBus _sharedEventBus;
        private EntityHandleRegistry _sharedHandleRegistry;

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
        /// Gets the SystemRunner instance.
        /// </summary>
        public static SystemRunner Systems { get; private set; }

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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_gameConfig == null)
            {
                Debug.LogError("[GameBootstrapper] No configuration assigned! Please assign a GameBootstrapperConfig.");
                return;
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

            Exception error = null;

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

            Log("Phase 2: Building Container");
            if (!TryExecute(() => BuildContainer(), "Container Building", out error))
            {
                HandleInitializationError(error);
                yield break;
            }

            Log("Phase 3: Creating ECS World");
            if (!TryExecute(() => CreateWorld(), "World Creation", out error))
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

            Log("Phase 5: System Initialization");
            if (!TryExecute(() => InitializeSystems(), "System Initialization", out error))
            {
                HandleInitializationError(error);
                yield break;
            }

            CompleteInitialization();
        }

        private void BuildContainer()
        {
            var builder = new ContainerBuilder();
            var moduleBuilder = new ModuleBuilder(builder);

            _sharedEventBus = new EventBus();
            _sharedHandleRegistry = new EntityHandleRegistry();
            builder.RegisterInstance(_sharedEventBus);
            builder.RegisterInstance(_sharedHandleRegistry);

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
            _world = new ECSBuilder()
                .WithEventBus(_sharedEventBus)
                .Build();
            ECS.World.World.Current = _world;

            _systemRunner = new SystemRunner(_world.EntityManager, _world.EventBus, _sharedHandleRegistry, _container);
            _systemRunner.AddSystemsFromConfigs(_gameConfig.GetEnabledModules());

            Log($"World created with {_systemRunner.SystemCount} systems");
        }

        private IEnumerator InitializeModulesAsync()
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

        private void CompleteInitialization()
        {
            _isInitialized = true;
            Container = _container;
            Services = _serviceLocator;
            World = _world;
            Systems = _systemRunner;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[GameBootstrapper] {phaseName} failed: {ex}");
#else
                Debug.LogError($"[GameBootstrapper] {phaseName} failed: {ex.Message}");
#endif
                return false;
            }
        }

        private void HandleInitializationError(Exception ex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Initialization failed: {ex}");
#else
            Debug.LogError($"[GameBootstrapper] Initialization failed: {ex.Message}");
#endif
            DisposeResources();
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

            DisposeResources();
            _isInitialized = false;

            PlayerLoop.Shutdown();

            Log("=== Strada Framework Shutdown Complete ===");
        }

        private void DisposeResources()
        {
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

            _systemRunner?.Dispose();
            _systemRunner = null;

            _world?.Dispose();
            _world = null;
            ECS.World.World.Current = null;

            if (_container is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _container = null;
            _serviceLocator = null;

            Container = null;
            Services = null;
            World = null;
            Systems = null;
        }

        private void Log(string message)
        {
            if (_gameConfig != null && _gameConfig.VerboseLogging)
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
        /// Gets the SystemRunner instance.
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
    }
}
