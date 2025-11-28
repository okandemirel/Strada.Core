using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.Systems;
using Strada.Core.ECS.World;
using Strada.Core.Sync;
using UnityEngine;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Config-driven system runner that instantiates and executes ECS systems
    /// based on ModuleConfig definitions. Replaces direct SystemScheduler usage
    /// for modular system configuration.
    /// </summary>
    public sealed class SystemRunner : IDisposable
    {
        private readonly List<SystemInstance>[] _systemsByPhase;
        private readonly List<SystemInstance> _allSystems;
        private readonly EntityManager _entityManager;
        private readonly MessageBus _messageBus;
        private readonly EntityHandleRegistry _handleRegistry;
        private readonly IContainer _container;
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Wrapper that holds system instance along with its configuration.
        /// </summary>
        private readonly struct SystemInstance
        {
            public readonly ISystem System;
            public readonly int Order;
            public readonly string Name;

            public SystemInstance(ISystem system, int order, string name)
            {
                System = system;
                Order = order;
                Name = name;
            }
        }

        /// <summary>
        /// Creates a new SystemRunner.
        /// </summary>
        /// <param name="entityManager">The entity manager for system injection.</param>
        /// <param name="messageBus">The message bus for system injection.</param>
        /// <param name="handleRegistry">The entity handle registry for system injection.</param>
        /// <param name="container">The DI container for resolving system dependencies.</param>
        public SystemRunner(EntityManager entityManager, MessageBus messageBus, EntityHandleRegistry handleRegistry, IContainer container = null)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _messageBus = messageBus;
            _handleRegistry = handleRegistry;
            _container = container;

            int phaseCount = Enum.GetValues(typeof(UpdatePhase)).Length;
            _systemsByPhase = new List<SystemInstance>[phaseCount];
            for (int i = 0; i < phaseCount; i++)
                _systemsByPhase[i] = new List<SystemInstance>(8);
            _allSystems = new List<SystemInstance>(32);
        }

        /// <summary>
        /// Gets the total number of registered systems.
        /// </summary>
        public int SystemCount => _allSystems.Count;

        /// <summary>
        /// Gets all registered systems.
        /// </summary>
        public IReadOnlyList<ISystem> GetAllSystems()
        {
            var result = new List<ISystem>(_allSystems.Count);
            foreach (var instance in _allSystems)
                result.Add(instance.System);
            return result;
        }

        /// <summary>
        /// Adds systems from a ModuleConfig to this runner.
        /// </summary>
        /// <param name="config">The module configuration containing system entries.</param>
        public void AddSystemsFromConfig(ModuleConfig config)
        {
            if (config == null || !config.Enabled)
                return;

            foreach (var entry in config.Systems)
            {
                if (!entry.Enabled || !entry.IsValid)
                    continue;

                var system = CreateSystem(entry);
                if (system != null)
                {
                    AddSystem(system, entry.Phase, entry.Order, entry.DisplayName);
                }
            }
        }

        /// <summary>
        /// Adds systems from multiple ModuleConfigs, ordered by their priority.
        /// </summary>
        /// <param name="configs">The module configurations to process.</param>
        public void AddSystemsFromConfigs(IEnumerable<ModuleConfig> configs)
        {
            foreach (var config in configs)
            {
                AddSystemsFromConfig(config);
            }
        }

        /// <summary>
        /// Adds a system instance directly.
        /// </summary>
        /// <param name="system">The system to add.</param>
        /// <param name="phase">The update phase for this system.</param>
        /// <param name="order">The execution order within the phase.</param>
        /// <param name="name">Optional display name for debugging.</param>
        public void AddSystem(ISystem system, UpdatePhase phase = UpdatePhase.Update, int order = 0, string name = null)
        {
            if (_initialized)
            {
                Debug.LogWarning("[SystemRunner] Adding system after initialization. System will be initialized immediately.");
                InjectSystem(system);
                system.Initialize();
            }

            var instance = new SystemInstance(system, order, name ?? system.GetType().Name);
            var phaseList = _systemsByPhase[(int)phase];

            int insertIndex = 0;
            for (int i = 0; i < phaseList.Count; i++)
            {
                if (phaseList[i].Order > order)
                    break;
                insertIndex++;
            }
            phaseList.Insert(insertIndex, instance);
            _allSystems.Add(instance);
        }

        /// <summary>
        /// Initializes all registered systems.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            foreach (var instance in _allSystems)
            {
                InjectSystem(instance.System);
            }

            var initSystems = _systemsByPhase[(int)UpdatePhase.Initialization];
            for (int i = 0; i < initSystems.Count; i++)
                initSystems[i].System.Initialize();

            for (int phase = 1; phase < _systemsByPhase.Length; phase++)
            {
                var systems = _systemsByPhase[phase];
                for (int i = 0; i < systems.Count; i++)
                    systems[i].System.Initialize();
            }
        }

        /// <summary>
        /// Updates systems in the Update phase.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float deltaTime)
        {
            var systems = _systemsByPhase[(int)UpdatePhase.Update];
            for (int i = 0; i < systems.Count; i++)
                systems[i].System.Update(deltaTime);
        }

        /// <summary>
        /// Updates systems in the LateUpdate phase.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LateUpdate(float deltaTime)
        {
            var systems = _systemsByPhase[(int)UpdatePhase.LateUpdate];
            for (int i = 0; i < systems.Count; i++)
                systems[i].System.Update(deltaTime);
        }

        /// <summary>
        /// Updates systems in the FixedUpdate phase.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixedUpdate(float fixedDeltaTime)
        {
            var systems = _systemsByPhase[(int)UpdatePhase.FixedUpdate];
            for (int i = 0; i < systems.Count; i++)
                systems[i].System.Update(fixedDeltaTime);
        }

        /// <summary>
        /// Disposes all systems in reverse order.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = _allSystems.Count - 1; i >= 0; i--)
                _allSystems[i].System.Dispose();

            _allSystems.Clear();
            for (int i = 0; i < _systemsByPhase.Length; i++)
                _systemsByPhase[i].Clear();
        }

        /// <summary>
        /// Gets debug information about registered systems.
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== SystemRunner Debug Info ===");
            sb.AppendLine($"Total Systems: {_allSystems.Count}");
            sb.AppendLine($"Initialized: {_initialized}");
            sb.AppendLine();

            string[] phaseNames = Enum.GetNames(typeof(UpdatePhase));
            for (int phase = 0; phase < _systemsByPhase.Length; phase++)
            {
                var systems = _systemsByPhase[phase];
                if (systems.Count == 0) continue;

                sb.AppendLine($"[{phaseNames[phase]}] ({systems.Count} systems):");
                for (int i = 0; i < systems.Count; i++)
                {
                    sb.AppendLine($"  {i + 1}. {systems[i].Name} (order: {systems[i].Order})");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private ISystem CreateSystem(SystemEntry entry)
        {
            var systemType = entry.GetSystemType();
            if (systemType == null)
            {
                Debug.LogWarning($"[SystemRunner] System type is null for entry: {entry.DisplayName}");
                return null;
            }

            if (_container != null && _container.IsRegistered(systemType))
            {
                return _container.Resolve(systemType) as ISystem;
            }

            return Activator.CreateInstance(systemType) as ISystem;
        }

        private void InjectSystem(ISystem system)
        {
            if (system is SystemBase systemBase)
            {
                systemBase.Inject(_entityManager, _messageBus, _handleRegistry);
            }
        }
    }
}
