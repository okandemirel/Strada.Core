using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Base class for all module configurations.
    /// This is the unified pattern for defining modules in Strada - replaces both IModuleInstaller and MonoBehaviour bootstrappers.
    ///
    /// Each module should create a ScriptableObject that inherits from ModuleConfig and configure:
    /// - Systems: ECS systems to register (can be configured in Inspector)
    /// - Services: DI services to register (can be configured in Inspector or code)
    /// - Custom settings: Module-specific configuration data
    /// </summary>
    public abstract class ModuleConfig : ScriptableObject
    {
        [Header("Module Identity")]
        [Tooltip("The name of this module (used for logging and discovery)")]
        [SerializeField] protected string _moduleName;

        [Tooltip("Initialization priority (lower values initialize first)")]
        [SerializeField] protected int _priority = 0;

        [Tooltip("Whether this module is enabled")]
        [SerializeField] protected bool _enabled = true;

        [Header("ECS Systems")]
        [Tooltip("Systems belonging to this module. Configure via Inspector or use 'Discover Systems' button.")]
        [SerializeField] protected List<SystemEntry> _systems = new();

        [Header("Services")]
        [Tooltip("Services to register with the DI container. Can also be registered in Install() method.")]
        [SerializeField] protected List<ServiceEntry> _services = new();

        [Header("Dependencies")]
        [Tooltip("Other modules this module depends on. Dependent modules will be initialized first.")]
        [SerializeField] protected List<ModuleConfig> _dependencies = new();

        /// <summary>
        /// Gets the module name. Falls back to asset name if not set.
        /// </summary>
        public string ModuleName => string.IsNullOrEmpty(_moduleName) ? name : _moduleName;

        /// <summary>
        /// Gets the initialization priority (lower values initialize first).
        /// </summary>
        public int Priority => _priority;

        /// <summary>
        /// Gets whether this module is enabled.
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Gets the list of systems configured for this module.
        /// </summary>
        public IReadOnlyList<SystemEntry> Systems => _systems;

        /// <summary>
        /// Gets the list of services configured for this module.
        /// </summary>
        public IReadOnlyList<ServiceEntry> Services => _services;

        /// <summary>
        /// Gets the list of modules this module depends on.
        /// </summary>
        public IReadOnlyList<ModuleConfig> Dependencies => _dependencies;

        /// <summary>
        /// Gets enabled systems sorted by order.
        /// </summary>
        public IEnumerable<SystemEntry> GetEnabledSystems()
        {
            return _systems
                .Where(s => s != null && s.Enabled && s.IsValid)
                .OrderBy(s => s.Order);
        }

        /// <summary>
        /// Gets enabled services.
        /// </summary>
        public IEnumerable<ServiceEntry> GetEnabledServices()
        {
            return _services
                .Where(s => s != null && s.Enabled && s.IsValid);
        }

        /// <summary>
        /// Called by the framework to install module dependencies.
        /// First registers Inspector-configured systems and services, then calls Configure().
        /// </summary>
        /// <param name="builder">The module builder for registering dependencies.</param>
        public void Install(IModuleBuilder builder)
        {
            builder.RegisterInstance(this);

            foreach (var service in GetEnabledServices())
            {
                var interfaceType = service.GetInterfaceType();
                var implType = service.GetImplementationType();

                if (interfaceType != null && implType != null)
                {
                    builder.Register(interfaceType, implType, service.Lifetime);
                }
            }

            Configure(builder);
        }

        /// <summary>
        /// Override to register custom dependencies. Called after Inspector-configured items are registered.
        /// </summary>
        /// <param name="builder">The module builder for registering dependencies.</param>
        protected virtual void Configure(IModuleBuilder builder) { }

        /// <summary>
        /// Called after the DI container is built and all modules are installed.
        /// Use for initialization logic that requires resolved services.
        /// </summary>
        /// <param name="services">The service locator for resolving dependencies.</param>
        public virtual void Initialize(IServiceLocator services) { }

        /// <summary>
        /// Called when the application shuts down. Use for cleanup.
        /// Modules are shut down in reverse order of initialization.
        /// </summary>
        public virtual void Shutdown() { }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            _systems?.RemoveAll(s => s == null);
            _services?.RemoveAll(s => s == null);
            _dependencies?.RemoveAll(d => d == null);

            if (string.IsNullOrEmpty(_moduleName))
            {
                _moduleName = name;
            }
        }

        /// <summary>
        /// Editor method: Adds a system entry to this module.
        /// </summary>
        public void EditorAddSystem(SystemEntry entry)
        {
            _systems ??= new List<SystemEntry>();
            _systems.Add(entry);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Editor method: Removes a system entry from this module.
        /// </summary>
        public void EditorRemoveSystem(SystemEntry entry)
        {
            _systems?.Remove(entry);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Editor method: Clears all systems from this module.
        /// </summary>
        public void EditorClearSystems()
        {
            _systems?.Clear();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Editor method: Checks if a system type is already added.
        /// </summary>
        public bool EditorHasSystem(Type systemType)
        {
            return _systems?.Any(s => s.SystemType?.Type == systemType) ?? false;
        }
#endif
    }
}
