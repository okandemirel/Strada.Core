using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Strada.Core.ECS
{
    /// <summary>
    /// Interface for a Strada ECS world.
    /// Worlds contain entities and systems, and manage their lifecycle.
    /// </summary>
    /// <remarks>
    /// A world is an isolated container for entities and systems.
    /// Different worlds can run independently with their own entities.
    ///
    /// Example usage:
    /// <code>
    /// var world = StradaWorld.Create("GameWorld");
    /// world.RegisterSystem&lt;MovementSystem&gt;();
    /// world.Initialize();
    /// world.Update();
    /// world.Dispose();
    /// </code>
    /// </remarks>
    public interface IStradaWorld : IDisposable
    {
        /// <summary>
        /// Gets the name of this world.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether this world has been initialized.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Gets whether this world has been disposed.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets the entity manager for this world.
        /// </summary>
        IEntityManager EntityManager { get; }

        /// <summary>
        /// Gets the number of registered systems.
        /// </summary>
        int SystemCount { get; }

        /// <summary>
        /// Registers a system type for auto-discovery.
        /// </summary>
        /// <typeparam name="T">The system type</typeparam>
        void RegisterSystem<T>() where T : struct, IStradaSystem;

        /// <summary>
        /// Registers a system type for auto-discovery.
        /// </summary>
        /// <param name="systemType">The system type</param>
        void RegisterSystem(Type systemType);

        /// <summary>
        /// Initializes the world and all registered systems.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Updates all systems in this world.
        /// </summary>
        /// <param name="deltaTime">Time since last update</param>
        void Update(float deltaTime);

        /// <summary>
        /// Gets a system by type.
        /// </summary>
        /// <typeparam name="T">The system type</typeparam>
        /// <returns>The system instance</returns>
        T GetSystem<T>() where T : struct, IStradaSystem;

        /// <summary>
        /// Checks if a system is registered.
        /// </summary>
        /// <typeparam name="T">The system type</typeparam>
        /// <returns>True if the system is registered</returns>
        bool HasSystem<T>() where T : struct, IStradaSystem;

        /// <summary>
        /// Enables or disables a system.
        /// </summary>
        /// <typeparam name="T">The system type</typeparam>
        /// <param name="enabled">Whether to enable or disable</param>
        void SetSystemEnabled<T>(bool enabled) where T : struct, IStradaSystem;
    }

    /// <summary>
    /// Default implementation of IStradaWorld.
    /// Wraps Unity DOTS World and provides Strada-specific functionality.
    /// </summary>
    public class StradaWorld : IStradaWorld
    {
        private readonly List<SystemRegistration> _registeredSystems;
        private readonly Dictionary<Type, object> _systemInstances;
        private bool _isInitialized;
        private bool _isDisposed;
        private readonly StradaEntityManager _entityManager;
        private double _totalTime;

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc/>
        public bool IsDisposed => _isDisposed;

        /// <inheritdoc/>
        public IEntityManager EntityManager => _entityManager;

        /// <inheritdoc/>
        public int SystemCount => _registeredSystems.Count;

        /// <summary>
        /// Initializes a new instance of StradaWorld.
        /// </summary>
        /// <param name="name">The name of this world</param>
        private StradaWorld(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("World name cannot be null or empty", nameof(name));

            Name = name;
            _registeredSystems = new List<SystemRegistration>();
            _systemInstances = new Dictionary<Type, object>();
            _entityManager = new StradaEntityManager();
            _isInitialized = false;
            _isDisposed = false;
            _totalTime = 0.0;
        }

        /// <summary>
        /// Creates a new Strada world.
        /// </summary>
        /// <param name="name">The name of the world</param>
        /// <returns>A new world instance</returns>
        public static IStradaWorld Create(string name)
        {
            return new StradaWorld(name);
        }

        /// <summary>
        /// Creates a new Strada world with auto-discovery of systems.
        /// </summary>
        /// <param name="name">The name of the world</param>
        /// <param name="assemblyFilter">Optional filter for assemblies to scan</param>
        /// <returns>A new world instance with auto-discovered systems</returns>
        public static IStradaWorld CreateWithAutoDiscovery(string name, Func<Assembly, bool> assemblyFilter = null)
        {
            var world = new StradaWorld(name);
            world.DiscoverAndRegisterSystems(assemblyFilter);
            return world;
        }

        /// <summary>
        /// Discovers and registers all systems marked with [StradaSystem] attribute.
        /// </summary>
        /// <param name="assemblyFilter">Optional filter for assemblies to scan</param>
        public void DiscoverAndRegisterSystems(Func<Assembly, bool> assemblyFilter = null)
        {
            if (_isInitialized)
                throw new InvalidOperationException("Cannot discover systems after world is initialized");

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                // Skip Unity and System assemblies by default
                if (assemblyFilter != null && !assemblyFilter(assembly))
                    continue;

                if (ShouldSkipAssembly(assembly))
                    continue;

                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (!type.IsValueType || !typeof(IStradaSystem).IsAssignableFrom(type))
                            continue;

                        var attribute = type.GetCustomAttribute<StradaSystemAttribute>();
                        if (attribute == null)
                            continue;

                        // Validate the attribute
                        attribute.Validate(type);

                        RegisterSystem(type, attribute);
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Log warning but continue
                    UnityEngine.Debug.LogWarning($"Failed to load types from assembly {assembly.FullName}: {ex.Message}");
                }
            }
        }

        private bool ShouldSkipAssembly(Assembly assembly)
        {
            var name = assembly.FullName;
            return name.StartsWith("Unity.") ||
                   name.StartsWith("UnityEngine.") ||
                   name.StartsWith("UnityEditor.") ||
                   name.StartsWith("System.") ||
                   name.StartsWith("mscorlib") ||
                   name.StartsWith("netstandard");
        }

        /// <inheritdoc/>
        public void RegisterSystem<T>() where T : struct, IStradaSystem
        {
            RegisterSystem(typeof(T));
        }

        /// <inheritdoc/>
        public void RegisterSystem(Type systemType)
        {
            if (_isInitialized)
                throw new InvalidOperationException("Cannot register systems after world is initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(StradaWorld));

            if (!systemType.IsValueType)
                throw new ArgumentException($"System type must be a struct: {systemType.Name}");

            if (!typeof(IStradaSystem).IsAssignableFrom(systemType))
                throw new ArgumentException($"System type must implement IStradaSystem: {systemType.Name}");

            // Check if already registered
            if (_registeredSystems.Any(s => s.SystemType == systemType))
                return;

            // Get or create attribute
            var attribute = systemType.GetCustomAttribute<StradaSystemAttribute>() ?? new StradaSystemAttribute();

            RegisterSystem(systemType, attribute);
        }

        private void RegisterSystem(Type systemType, StradaSystemAttribute attribute)
        {
            var registration = new SystemRegistration
            {
                SystemType = systemType,
                Attribute = attribute,
                UpdateGroup = attribute.UpdateInGroup ?? typeof(SimulationSystemGroup),
                Priority = attribute.Priority,
                Enabled = attribute.EnabledByDefault
            };

            _registeredSystems.Add(registration);
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (_isInitialized)
                throw new InvalidOperationException("World is already initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(StradaWorld));

            // Sort systems by priority and dependencies
            var sortedSystems = TopologicalSort(_registeredSystems);

            // Create and initialize system instances
            foreach (var registration in sortedSystems)
            {
                try
                {
                    // Create system instance
                    var systemInstance = Activator.CreateInstance(registration.SystemType);
                    _systemInstances[registration.SystemType] = systemInstance;

                    // Call OnCreate
                    var system = (IStradaSystem)systemInstance;
                    var state = CreateSystemState();
                    system.OnCreate(ref state);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to initialize system {registration.SystemType.Name}: {ex.Message}", ex);
                }
            }

            // Update registration list to sorted order
            _registeredSystems.Clear();
            _registeredSystems.AddRange(sortedSystems);

            _isInitialized = true;
        }

        /// <inheritdoc/>
        public void Update(float deltaTime)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("World is not initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(StradaWorld));

            _totalTime += deltaTime;

            // Update systems in order
            foreach (var registration in _registeredSystems)
            {
                if (!registration.Enabled)
                    continue;

                try
                {
                    var systemInstance = _systemInstances[registration.SystemType];
                    var system = (IStradaSystem)systemInstance;

                    var state = CreateSystemState(deltaTime);
                    state.Enabled = registration.Enabled;

                    system.OnUpdate(ref state);

                    // Update enabled state (system might have disabled itself)
                    registration.Enabled = state.Enabled;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Error updating system {registration.SystemType.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        /// <inheritdoc/>
        public T GetSystem<T>() where T : struct, IStradaSystem
        {
            if (!_isInitialized)
                throw new InvalidOperationException("World is not initialized");

            if (!_systemInstances.TryGetValue(typeof(T), out var instance))
                throw new InvalidOperationException($"System {typeof(T).Name} is not registered");

            return (T)instance;
        }

        /// <inheritdoc/>
        public bool HasSystem<T>() where T : struct, IStradaSystem
        {
            return _systemInstances.ContainsKey(typeof(T));
        }

        /// <inheritdoc/>
        public void SetSystemEnabled<T>(bool enabled) where T : struct, IStradaSystem
        {
            var registration = _registeredSystems.FirstOrDefault(s => s.SystemType == typeof(T));
            if (registration == null)
                throw new InvalidOperationException($"System {typeof(T).Name} is not registered");

            registration.Enabled = enabled;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            // Call OnDestroy on all systems in reverse order
            for (int i = _registeredSystems.Count - 1; i >= 0; i--)
            {
                var registration = _registeredSystems[i];
                try
                {
                    if (_systemInstances.TryGetValue(registration.SystemType, out var instance))
                    {
                        var system = (IStradaSystem)instance;
                        var state = CreateSystemState();
                        system.OnDestroy(ref state);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Error destroying system {registration.SystemType.Name}: {ex.Message}");
                }
            }

            _systemInstances.Clear();
            _registeredSystems.Clear();
            _isDisposed = true;
        }

        private SystemState CreateSystemState(float deltaTime = 0f)
        {
            return new SystemState
            {
                EntityManager = _entityManager,
                DeltaTime = deltaTime,
                Time = _totalTime,
                Enabled = true
            };
        }

        private List<SystemRegistration> TopologicalSort(List<SystemRegistration> systems)
        {
            // Build dependency graph
            var graph = new Dictionary<Type, List<Type>>();
            var inDegree = new Dictionary<Type, int>();

            foreach (var system in systems)
            {
                graph[system.SystemType] = new List<Type>();
                inDegree[system.SystemType] = 0;
            }

            foreach (var system in systems)
            {
                if (system.Attribute.UpdateAfter != null)
                {
                    foreach (var dependency in system.Attribute.UpdateAfter)
                    {
                        if (graph.ContainsKey(dependency))
                        {
                            graph[dependency].Add(system.SystemType);
                            inDegree[system.SystemType]++;
                        }
                    }
                }

                if (system.Attribute.UpdateBefore != null)
                {
                    foreach (var dependency in system.Attribute.UpdateBefore)
                    {
                        if (graph.ContainsKey(dependency))
                        {
                            graph[system.SystemType].Add(dependency);
                            inDegree[dependency]++;
                        }
                    }
                }
            }

            // Kahn's algorithm for topological sort
            var queue = new Queue<Type>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                    queue.Enqueue(kvp.Key);
            }

            var sorted = new List<SystemRegistration>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var registration = systems.First(s => s.SystemType == current);
                sorted.Add(registration);

                foreach (var neighbor in graph[current])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            // Check for circular dependencies
            if (sorted.Count != systems.Count)
            {
                var remaining = systems.Where(s => !sorted.Contains(s)).Select(s => s.SystemType.Name);
                throw new InvalidOperationException(
                    $"Circular dependency detected in systems: {string.Join(", ", remaining)}");
            }

            return sorted;
        }

        private class SystemRegistration
        {
            public Type SystemType { get; set; }
            public StradaSystemAttribute Attribute { get; set; }
            public Type UpdateGroup { get; set; }
            public int Priority { get; set; }
            public bool Enabled { get; set; }
        }
    }

    /// <summary>
    /// Simple entity manager implementation for testing.
    /// In production, this would wrap Unity's EntityManager.
    /// </summary>
    internal class StradaEntityManager : IEntityManager
    {
        private int _nextEntityId = 1;
        private readonly Dictionary<Entity, Dictionary<Type, object>> _entityComponents;

        public StradaEntityManager()
        {
            _entityComponents = new Dictionary<Entity, Dictionary<Type, object>>();
        }

        public Entity CreateEntity()
        {
            var entity = new Entity { Index = _nextEntityId++, Version = 1 };
            _entityComponents[entity] = new Dictionary<Type, object>();
            return entity;
        }

        public Entity CreateEntity(EntityArchetype archetype)
        {
            // Simplified - just create entity
            return CreateEntity();
        }

        public void DestroyEntity(Entity entity)
        {
            _entityComponents.Remove(entity);
        }

        public bool Exists(Entity entity)
        {
            return _entityComponents.ContainsKey(entity);
        }

        public void AddComponent<T>(Entity entity) where T : struct, IStradaComponent
        {
            if (!_entityComponents.ContainsKey(entity))
                throw new ArgumentException($"Entity {entity.Index} does not exist");

            _entityComponents[entity][typeof(T)] = default(T);
        }

        public void RemoveComponent<T>(Entity entity) where T : struct, IStradaComponent
        {
            if (_entityComponents.TryGetValue(entity, out var components))
            {
                components.Remove(typeof(T));
            }
        }

        public bool HasComponent<T>(Entity entity) where T : struct, IStradaComponent
        {
            return _entityComponents.TryGetValue(entity, out var components) &&
                   components.ContainsKey(typeof(T));
        }

        public T GetComponent<T>(Entity entity) where T : struct, IStradaComponent
        {
            if (!_entityComponents.TryGetValue(entity, out var components) ||
                !components.TryGetValue(typeof(T), out var component))
            {
                throw new InvalidOperationException($"Entity {entity.Index} does not have component {typeof(T).Name}");
            }

            return (T)component;
        }

        public void SetComponent<T>(Entity entity, T component) where T : struct, IStradaComponent
        {
            if (!_entityComponents.TryGetValue(entity, out var components))
                throw new ArgumentException($"Entity {entity.Index} does not exist");

            components[typeof(T)] = component;
        }
    }
}
