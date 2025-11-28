using System;
using UnityEngine;
using Strada.Core.DI;
using Strada.Core.Data;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Base class for module bootstrappers. Each module should have its own bootstrapper
    /// MonoBehaviour that holds config references and registers with the main container.
    /// Similar to VContainer's LifetimeScope or Zenject's SceneContext.
    /// </summary>
    public abstract class ModuleBootstrapper : MonoBehaviour
    {
        [Header("Module Settings")]
        [SerializeField] private bool _autoInitialize = true;
        [SerializeField] private int _priority;

        private IContainer _container;
        private bool _initialized;

        /// <summary>
        /// Gets the module's container reference after initialization.
        /// </summary>
        public IContainer Container => _container;

        /// <summary>
        /// Gets whether this module has been initialized.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Gets the module initialization priority. Lower values initialize first.
        /// </summary>
        public int Priority => _priority;

        /// <summary>
        /// Event raised when module initialization completes.
        /// </summary>
        public event Action OnModuleInitialized;

        protected virtual void Awake()
        {
            if (_autoInitialize)
            {
                // Will be called by GameBootstrapper if using auto-discovery
                // Otherwise can be manually triggered
            }
        }

        /// <summary>
        /// Initializes the module with the given parent container.
        /// Called by GameBootstrapper or can be called manually.
        /// </summary>
        public void Initialize(IContainer parentContainer)
        {
            if (_initialized)
            {
                Debug.LogWarning($"[{GetType().Name}] Module already initialized.");
                return;
            }

            _container = parentContainer;

            try
            {
                OnConfigure(parentContainer);
                OnInitialize(parentContainer);
                _initialized = true;
                OnModuleInitialized?.Invoke();

                Debug.Log($"[{GetType().Name}] Module initialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] Module initialization failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Installs module dependencies into the container builder.
        /// Called during the container build phase.
        /// </summary>
        public void Install(IContainerBuilder builder)
        {
            OnInstall(builder);
        }

        /// <summary>
        /// Override to register module dependencies with the container builder.
        /// This is called before the container is built.
        /// </summary>
        protected abstract void OnInstall(IContainerBuilder builder);

        /// <summary>
        /// Override to configure the module after container is available but before full initialization.
        /// Use this to resolve and configure dependencies.
        /// </summary>
        protected virtual void OnConfigure(IContainer container) { }

        /// <summary>
        /// Override to perform post-construction initialization.
        /// Called after all dependencies are registered and container is built.
        /// </summary>
        protected virtual void OnInitialize(IContainer container) { }

        /// <summary>
        /// Override to clean up module resources during shutdown.
        /// </summary>
        protected virtual void OnShutdown() { }

        protected virtual void OnDestroy()
        {
            if (_initialized)
            {
                OnShutdown();
                _initialized = false;
            }
        }
    }

    /// <summary>
    /// Generic module bootstrapper with typed config support.
    /// </summary>
    /// <typeparam name="TConfig">The module's config ScriptableObject type.</typeparam>
    public abstract class ModuleBootstrapper<TConfig> : ModuleBootstrapper
        where TConfig : ConfigData
    {
        [Header("Module Config")]
        [SerializeField] protected TConfig _config;

        /// <summary>
        /// Gets the module's configuration asset.
        /// </summary>
        public TConfig Config => _config;

        protected override void OnInstall(IContainerBuilder builder)
        {
            if (_config != null)
            {
                builder.RegisterInstance(_config);
            }

            InstallBindings(builder);
        }

        /// <summary>
        /// Override to register module-specific bindings.
        /// Config is already registered when this is called.
        /// </summary>
        protected abstract void InstallBindings(IContainerBuilder builder);
    }
}
