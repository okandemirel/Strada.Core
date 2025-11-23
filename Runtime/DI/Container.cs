using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Strada.Core.DI
{
    /// <summary>
    /// High-performance dependency injection container.
    /// </summary>
    /// <remarks>
    /// Immutable after construction for thread-safety.
    /// Optimized for zero-allocation resolution of singletons.
    /// </remarks>
    public class Container : IContainer
    {
        private readonly Dictionary<Type, Registration> _registrations;
        private readonly Dictionary<Type, object> _singletonCache;
        private readonly HashSet<Type> _resolvingTypes; // Circular dependency detection
        private readonly object _lockObject = new object();
        private bool _disposed;

        /// <summary>
        /// Creates a new container with the given registrations.
        /// </summary>
        internal Container(Dictionary<Type, Registration> registrations)
        {
            _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
            _singletonCache = new Dictionary<Type, object>();
            _resolvingTypes = new HashSet<Type>();

            // Pre-cache constructor info for performance
            CacheConstructors();

            // Pre-populate singleton cache with registered instances
            PreCacheInstances();
        }

        /// <summary>
        /// Resolves an instance of the specified type.
        /// </summary>
        public T Resolve<T>() where T : class
        {
            return (T)Resolve(typeof(T));
        }

        /// <summary>
        /// Resolves an instance of the specified type.
        /// </summary>
        public object Resolve(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (_disposed)
                throw new ObjectDisposedException(nameof(Container));

            if (!_registrations.TryGetValue(type, out var registration))
            {
                throw new InvalidOperationException(
                    $"Type '{type.Name}' is not registered in the container. " +
                    $"Register it using ContainerBuilder.Register<{type.Name}>() before building the container.");
            }

            return ResolveRegistration(registration, type);
        }

        /// <summary>
        /// Attempts to resolve an instance of the specified type.
        /// </summary>
        public bool TryResolve<T>(out T instance) where T : class
        {
            try
            {
                instance = Resolve<T>();
                return true;
            }
            catch
            {
                instance = null;
                return false;
            }
        }

        /// <summary>
        /// Creates a child scope with its own lifetime.
        /// </summary>
        public IContainerScope CreateScope()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Container));

            return new ContainerScope(this, _registrations);
        }

        /// <summary>
        /// Checks if a type is registered in the container.
        /// </summary>
        public bool IsRegistered<T>() where T : class
        {
            return IsRegistered(typeof(T));
        }

        /// <summary>
        /// Checks if a type is registered in the container.
        /// </summary>
        public bool IsRegistered(Type type)
        {
            return _registrations.ContainsKey(type);
        }

        /// <summary>
        /// Resolves a registration to an instance.
        /// </summary>
        private object ResolveRegistration(Registration registration, Type requestedType)
        {
            // Handle singleton lifetime
            if (registration.Lifetime == Lifetime.Singleton)
            {
                // Check cache first (zero allocation path)
                if (_singletonCache.TryGetValue(requestedType, out var cachedInstance))
                {
                    return cachedInstance;
                }

                // Thread-safe singleton creation
                lock (_lockObject)
                {
                    // Double-check after acquiring lock
                    if (_singletonCache.TryGetValue(requestedType, out cachedInstance))
                    {
                        return cachedInstance;
                    }

                    var instance = CreateInstance(registration, requestedType);
                    _singletonCache[requestedType] = instance;
                    return instance;
                }
            }

            // Handle scoped lifetime - cannot resolve from root container
            if (registration.Lifetime == Lifetime.Scoped)
            {
                throw new InvalidOperationException(
                    $"Cannot resolve scoped type '{requestedType.Name}' from root container. " +
                    $"Use CreateScope() first to create a scope, then resolve from the scope.");
            }

            // Handle transient lifetime
            return CreateInstance(registration, requestedType);
        }

        /// <summary>
        /// Creates a new instance from a registration.
        /// </summary>
        private object CreateInstance(Registration registration, Type requestedType)
        {
            // Pre-created instance
            if (registration.Instance != null)
            {
                return registration.Instance;
            }

            // Factory function
            if (registration.Factory != null)
            {
                try
                {
                    return registration.Factory(this);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Factory for type '{requestedType.Name}' threw an exception", ex);
                }
            }

            // Constructor injection
            return CreateInstanceViaConstructor(registration, requestedType);
        }

        /// <summary>
        /// Creates an instance using constructor injection.
        /// </summary>
        private object CreateInstanceViaConstructor(Registration registration, Type requestedType)
        {
            // Circular dependency detection
            if (_resolvingTypes.Contains(requestedType))
            {
                var chain = string.Join(" -> ", _resolvingTypes.Select(t => t.Name));
                throw new InvalidOperationException(
                    $"Circular dependency detected: {chain} -> {requestedType.Name}");
            }

            _resolvingTypes.Add(requestedType);

            try
            {
                var constructor = registration.Constructor;
                if (constructor == null)
                {
                    throw new InvalidOperationException(
                        $"No suitable constructor found for type '{registration.ImplementationType.Name}'");
                }

                var parameters = constructor.GetParameters();
                if (parameters.Length == 0)
                {
                    // Parameterless constructor - fast path
                    return Activator.CreateInstance(registration.ImplementationType);
                }

                // Resolve constructor parameters
                var args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;

                    if (!IsRegistered(paramType))
                    {
                        throw new InvalidOperationException(
                            $"Cannot resolve type '{requestedType.Name}': " +
                            $"Constructor parameter '{parameters[i].Name}' of type '{paramType.Name}' is not registered. " +
                            $"Register it using ContainerBuilder.Register<{paramType.Name}>() before building the container.");
                    }

                    args[i] = Resolve(paramType);
                }

                return constructor.Invoke(args);
            }
            finally
            {
                _resolvingTypes.Remove(requestedType);
            }
        }

        /// <summary>
        /// Caches constructor information for all registrations.
        /// </summary>
        private void CacheConstructors()
        {
            foreach (var kvp in _registrations)
            {
                var registration = kvp.Value;

                // Skip if instance or factory
                if (registration.Instance != null || registration.Factory != null)
                    continue;

                // Find best constructor (prefer one with most parameters that can be resolved)
                var constructors = registration.ImplementationType
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .OrderByDescending(c => c.GetParameters().Length)
                    .ToArray();

                if (constructors.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Type '{registration.ImplementationType.Name}' has no public constructors");
                }

                // Use the first constructor (most parameters)
                registration.Constructor = constructors[0];
            }
        }

        /// <summary>
        /// Pre-populates the singleton cache with registered instances.
        /// </summary>
        private void PreCacheInstances()
        {
            foreach (var kvp in _registrations)
            {
                var type = kvp.Key;
                var registration = kvp.Value;

                if (registration.Instance != null)
                {
                    _singletonCache[type] = registration.Instance;
                }
            }
        }

        /// <summary>
        /// Disposes the container and all singleton instances that implement IDisposable.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Dispose all cached singletons
            foreach (var instance in _singletonCache.Values)
            {
                if (instance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                        // Suppress disposal exceptions to prevent cascading failures
                    }
                }
            }

            _singletonCache.Clear();
            _resolvingTypes.Clear();
        }
    }
}
