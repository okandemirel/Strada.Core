using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Strada.Core.ECS
{
    internal class SystemRegistration
    {
        public Type SystemType { get; set; }
        public StradaSystemAttribute Attribute { get; set; }
        public Type UpdateGroup { get; set; }
        public int Priority { get; set; }
        public bool Enabled { get; set; }
    }

    internal class SystemRegistry
    {
        private readonly List<SystemRegistration> _registrations = new List<SystemRegistration>();
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        public int Count => _registrations.Count;

        public void DiscoverAndRegister(Func<Assembly, bool> assemblyFilter = null)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                if (assemblyFilter != null && !assemblyFilter(assembly))
                    continue;

                if (ShouldSkipAssembly(assembly))
                    continue;

                DiscoverSystemsInAssembly(assembly);
            }
        }

        public void Register(Type systemType)
        {
            if (!systemType.IsValueType)
                throw new ArgumentException($"System type must be a struct: {systemType.Name}");

            if (!typeof(IStradaSystem).IsAssignableFrom(systemType))
                throw new ArgumentException($"System type must implement IStradaSystem: {systemType.Name}");

            if (_registrations.Any(s => s.SystemType == systemType))
                return;

            var attribute = systemType.GetCustomAttribute<StradaSystemAttribute>() ?? new StradaSystemAttribute();

            var registration = new SystemRegistration
            {
                SystemType = systemType,
                Attribute = attribute,
                UpdateGroup = attribute.UpdateInGroup ?? typeof(SimulationSystemGroup),
                Priority = attribute.Priority,
                Enabled = attribute.EnabledByDefault
            };

            _registrations.Add(registration);
        }

        public void Initialize(Func<SystemState> createState)
        {
            var sorted = SystemExecutor.TopologicalSort(_registrations);

            foreach (var registration in sorted)
            {
                var systemInstance = Activator.CreateInstance(registration.SystemType);
                _instances[registration.SystemType] = systemInstance;

                var system = (IStradaSystem)systemInstance;
                var state = createState();
                system.OnCreate(ref state);
            }

            _registrations.Clear();
            _registrations.AddRange(sorted);
        }

        public void Update(Func<SystemState> createState)
        {
            foreach (var registration in _registrations)
            {
                if (!registration.Enabled)
                    continue;

                var systemInstance = _instances[registration.SystemType];
                var system = (IStradaSystem)systemInstance;

                var state = createState();
                state.Enabled = registration.Enabled;

                system.OnUpdate(ref state);

                registration.Enabled = state.Enabled;
            }
        }

        public void Destroy(Func<SystemState> createState)
        {
            for (int i = _registrations.Count - 1; i >= 0; i--)
            {
                var registration = _registrations[i];
                if (_instances.TryGetValue(registration.SystemType, out var instance))
                {
                    var system = (IStradaSystem)instance;
                    var state = createState();
                    system.OnDestroy(ref state);
                }
            }

            _instances.Clear();
            _registrations.Clear();
        }

        public T GetSystem<T>() where T : struct, IStradaSystem
        {
            if (!_instances.TryGetValue(typeof(T), out var instance))
                throw new InvalidOperationException($"System {typeof(T).Name} is not registered");

            return (T)instance;
        }

        public bool HasSystem<T>() where T : struct, IStradaSystem
        {
            return _instances.ContainsKey(typeof(T));
        }

        public void SetSystemEnabled<T>(bool enabled) where T : struct, IStradaSystem
        {
            var registration = _registrations.FirstOrDefault(s => s.SystemType == typeof(T));
            if (registration == null)
                throw new InvalidOperationException($"System {typeof(T).Name} is not registered");

            registration.Enabled = enabled;
        }

        private void DiscoverSystemsInAssembly(Assembly assembly)
        {
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

                    attribute.Validate(type);
                    Register(type);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to load types from assembly {assembly.FullName}: {ex.Message}");
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
    }
}
