using System;
using System.Collections.Generic;
using System.Linq;

namespace Strada.Core.DI
{
    /// <summary>
    /// Scoped container with isolated lifetime for scoped registrations.
    /// </summary>
    /// <remarks>
    /// Scoped instances are created once per scope and disposed when the scope is disposed.
    /// Singleton instances are shared with the parent container.
    /// Transient instances are created fresh on each resolution.
    /// </remarks>
    public class ContainerScope : IContainerScope
    {
        private readonly IContainer _parent;
        private readonly Dictionary<Type, Registration> _registrations;
        private readonly Dictionary<Type, object> _scopedCache;
        private readonly HashSet<Type> _resolvingTypes; // Circular dependency detection
        private readonly object _lockObject = new object();
        private bool _disposed;

        /// <summary>
        /// Gets the parent container that created this scope.
        /// </summary>
        public IContainer Parent => _parent;

        /// <summary>
        /// Creates a new scoped container.
        /// </summary>
        internal ContainerScope(IContainer parent, Dictionary<Type, Registration> registrations)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
            _scopedCache = new Dictionary<Type, object>();
            _resolvingTypes = new HashSet<Type>();
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
                throw new ObjectDisposedException(nameof(ContainerScope));

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
                throw new ObjectDisposedException(nameof(ContainerScope));

            // Child scopes share the same registrations
            return new ContainerScope(_parent, _registrations);
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
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _registrations.ContainsKey(type);
        }

        /// <summary>
        /// Resolves a registration to an instance.
        /// </summary>
        private object ResolveRegistration(Registration registration, Type requestedType)
        {
            // Handle different lifetimes
            switch (registration.Lifetime)
            {
                case Lifetime.Singleton:
                    // Singletons are resolved from the parent container
                    return _parent.Resolve(requestedType);

                case Lifetime.Scoped:
                    // Scoped instances are cached within this scope
                    return ResolveScopedInstance(registration, requestedType);

                case Lifetime.Transient:
                    // Transients are always created fresh
                    return CreateInstance(registration, requestedType);

                default:
                    throw new InvalidOperationException($"Unknown lifetime: {registration.Lifetime}");
            }
        }

        /// <summary>
        /// Resolves a scoped instance (cached per scope).
        /// </summary>
        private object ResolveScopedInstance(Registration registration, Type requestedType)
        {
            // Check cache first (zero allocation path)
            if (_scopedCache.TryGetValue(requestedType, out var cachedInstance))
            {
                return cachedInstance;
            }

            // Thread-safe scoped instance creation
            lock (_lockObject)
            {
                // Double-check after acquiring lock
                if (_scopedCache.TryGetValue(requestedType, out cachedInstance))
                {
                    return cachedInstance;
                }

                var instance = CreateInstance(registration, requestedType);
                _scopedCache[requestedType] = instance;
                return instance;
            }
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
        /// Disposes the scope and all scoped instances that implement IDisposable.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Dispose all scoped instances
            foreach (var instance in _scopedCache.Values)
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

            _scopedCache.Clear();
            _resolvingTypes.Clear();
        }
    }
}
