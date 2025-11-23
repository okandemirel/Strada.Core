using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.ECS;

namespace Strada.Core.DI
{
    /// <summary>
    /// Builder for configuring and creating a dependency injection container.
    /// </summary>
    public class ContainerBuilder : IECSContainerBuilder
    {
        private readonly Dictionary<Type, Registration> _registrations;
        private readonly Dictionary<string, List<Type>> _ecsWorlds;

        /// <summary>
        /// Creates a new container builder.
        /// </summary>
        public ContainerBuilder()
        {
            _registrations = new Dictionary<Type, Registration>();
            _ecsWorlds = new Dictionary<string, List<Type>>();
        }

        /// <summary>
        /// Registers a type with its implementation.
        /// </summary>
        public IContainerBuilder Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Transient)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            var serviceType = typeof(TInterface);
            var implementationType = typeof(TImplementation);

            ValidateRegistration(serviceType, implementationType);

            var registration = new Registration(serviceType, implementationType, lifetime);
            _registrations[serviceType] = registration;

            return this;
        }

        /// <summary>
        /// Registers a concrete type.
        /// </summary>
        public IContainerBuilder Register<T>(Lifetime lifetime = Lifetime.Transient) where T : class
        {
            var type = typeof(T);

            ValidateConcreteType(type);

            var registration = new Registration(type, type, lifetime);
            _registrations[type] = registration;

            return this;
        }

        /// <summary>
        /// Registers a type with a factory function.
        /// </summary>
        public IContainerBuilder RegisterFactory<T>(Func<IContainer, T> factory, Lifetime lifetime = Lifetime.Transient)
            where T : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var serviceType = typeof(T);

            // Wrap the typed factory in an object-returning factory
            Func<IContainer, object> objectFactory = container => factory(container);

            var registration = new Registration(serviceType, objectFactory, lifetime);
            _registrations[serviceType] = registration;

            return this;
        }

        /// <summary>
        /// Registers a specific instance (always singleton).
        /// </summary>
        public IContainerBuilder RegisterInstance<T>(T instance) where T : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var serviceType = typeof(T);
            var registration = new Registration(serviceType, instance);
            _registrations[serviceType] = registration;

            return this;
        }

        /// <summary>
        /// Builds the immutable container from registered types.
        /// </summary>
        public IContainer Build()
        {
            // Validate all registrations
            foreach (var registration in _registrations.Values)
            {
                registration.Validate();
            }

            // Detect circular dependencies
            DetectCircularDependencies();

            // Create immutable container
            // Pass a copy of registrations to prevent external modification
            var registrationsCopy = new Dictionary<Type, Registration>(_registrations);
            return new Container(registrationsCopy);
        }

        /// <summary>
        /// Validates that a registration is valid.
        /// </summary>
        private void ValidateRegistration(Type serviceType, Type implementationType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (implementationType == null)
                throw new ArgumentNullException(nameof(implementationType));

            if (!serviceType.IsAssignableFrom(implementationType))
            {
                throw new ArgumentException(
                    $"Type '{implementationType.Name}' does not implement interface '{serviceType.Name}'");
            }

            if (implementationType.IsAbstract || implementationType.IsInterface)
            {
                throw new ArgumentException(
                    $"Implementation type '{implementationType.Name}' cannot be abstract or an interface");
            }
        }

        /// <summary>
        /// Validates that a concrete type can be instantiated.
        /// </summary>
        private void ValidateConcreteType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (type.IsAbstract || type.IsInterface)
            {
                throw new ArgumentException(
                    $"Type '{type.Name}' cannot be abstract or an interface. Use Register<TInterface, TImplementation>() instead.");
            }
        }

        /// <summary>
        /// Detects circular dependencies in the registration graph.
        /// </summary>
        private void DetectCircularDependencies()
        {
            var visited = new HashSet<Type>();
            var recursionStack = new Stack<Type>();

            foreach (var type in _registrations.Keys)
            {
                if (!visited.Contains(type))
                {
                    if (HasCircularDependency(type, visited, recursionStack))
                    {
                        var cycle = string.Join(" -> ", recursionStack.Reverse().Select(t => t.Name));
                        throw new InvalidOperationException(
                            $"Circular dependency detected: {cycle}. " +
                            $"Circular dependencies are not supported. Consider using factory registration or redesigning your dependencies.");
                    }
                }
            }
        }

        /// <summary>
        /// Recursively checks for circular dependencies using DFS.
        /// </summary>
        private bool HasCircularDependency(Type type, HashSet<Type> visited, Stack<Type> recursionStack)
        {
            visited.Add(type);
            recursionStack.Push(type);

            if (_registrations.TryGetValue(type, out var registration))
            {
                // Skip factory and instance registrations (no constructor dependencies)
                if (registration.Factory != null || registration.Instance != null)
                {
                    recursionStack.Pop();
                    return false;
                }

                // Get constructor parameters
                var constructors = registration.ImplementationType
                    .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (constructors.Length > 0)
                {
                    var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
                    var parameters = constructor.GetParameters();

                    foreach (var param in parameters)
                    {
                        var paramType = param.ParameterType;

                        // If this parameter is in our recursion stack, we have a cycle
                        if (recursionStack.Contains(paramType))
                        {
                            recursionStack.Push(paramType); // Add for complete cycle display
                            return true;
                        }

                        // If this parameter is registered, check it recursively
                        if (_registrations.ContainsKey(paramType))
                        {
                            if (!visited.Contains(paramType))
                            {
                                if (HasCircularDependency(paramType, visited, recursionStack))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            recursionStack.Pop();
            return false;
        }

        public IECSContainerBuilder RegisterWorld(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName))
                throw new ArgumentException("World name cannot be null or empty", nameof(worldName));

            if (!_ecsWorlds.ContainsKey(worldName))
            {
                _ecsWorlds[worldName] = new List<Type>();
            }

            RegisterFactory<IStradaWorld>(container =>
            {
                var world = StradaWorld.Create(worldName);
                if (_ecsWorlds.TryGetValue(worldName, out var systemTypes))
                {
                    foreach (var systemType in systemTypes)
                    {
                        world.RegisterSystem(systemType);
                    }
                }
                world.Initialize();
                return world;
            }, Lifetime.Singleton);

            return this;
        }

        public IECSContainerBuilder RegisterSystem<TSystem>(string worldName) where TSystem : IStradaSystem
        {
            return RegisterSystem(typeof(TSystem), worldName);
        }

        public IECSContainerBuilder RegisterSystem(Type systemType, string worldName)
        {
            if (systemType == null)
                throw new ArgumentNullException(nameof(systemType));

            if (string.IsNullOrWhiteSpace(worldName))
                throw new ArgumentException("World name cannot be null or empty", nameof(worldName));

            if (!typeof(IStradaSystem).IsAssignableFrom(systemType))
                throw new ArgumentException($"Type {systemType.Name} must implement IStradaSystem");

            if (!_ecsWorlds.ContainsKey(worldName))
            {
                _ecsWorlds[worldName] = new List<Type>();
            }

            _ecsWorlds[worldName].Add(systemType);

            return this;
        }
    }
}
