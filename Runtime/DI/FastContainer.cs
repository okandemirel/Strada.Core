using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Strada.Core.DI
{
    public sealed class FastContainer : IContainer
    {
        private readonly Dictionary<int, Func<IContainer, object>> _factories;
        private readonly Dictionary<int, object> _singletons;
        private readonly Dictionary<Type, int> _typeToHash;
        private readonly HashSet<int> _singletonHashes;
        private readonly HashSet<int> _resolving;
        private readonly object _lock = new object();
        private bool _disposed;

        internal FastContainer(Dictionary<Type, Registration> registrations)
        {
            var capacity = registrations.Count;
            _factories = new Dictionary<int, Func<IContainer, object>>(capacity);
            _singletons = new Dictionary<int, object>(capacity);
            _typeToHash = new Dictionary<Type, int>(capacity);
            _singletonHashes = new HashSet<int>();
            _resolving = new HashSet<int>();

            BuildFactories(registrations);
        }

        public T Resolve<T>() where T : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastContainer));

            var hash = GetTypeHash(typeof(T));

            if (_singletons.TryGetValue(hash, out var singleton))
                return (T)singleton;

            if (_factories.TryGetValue(hash, out var factory))
            {
                if (_singletonHashes.Contains(hash))
                {
                    lock (_lock)
                    {
                        if (_singletons.TryGetValue(hash, out singleton))
                            return (T)singleton;

                        var instance = factory(this);
                        _singletons[hash] = instance;
                        return (T)instance;
                    }
                }

                return (T)factory(this);
            }

            throw new InvalidOperationException($"Type '{typeof(T).Name}' is not registered");
        }

        public object Resolve(Type type)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastContainer));

            var hash = GetTypeHash(type);

            if (_singletons.TryGetValue(hash, out var singleton))
                return singleton;

            if (_factories.TryGetValue(hash, out var factory))
            {
                if (_singletonHashes.Contains(hash))
                {
                    lock (_lock)
                    {
                        if (_singletons.TryGetValue(hash, out singleton))
                            return singleton;

                        var instance = factory(this);
                        _singletons[hash] = instance;
                        return instance;
                    }
                }

                return factory(this);
            }

            throw new InvalidOperationException($"Type '{type.Name}' is not registered");
        }

        public bool TryResolve<T>(out T instance) where T : class
        {
            var hash = GetTypeHash(typeof(T));

            if (_singletons.TryGetValue(hash, out var singleton))
            {
                instance = (T)singleton;
                return true;
            }

            if (_factories.TryGetValue(hash, out var factory))
            {
                instance = (T)factory(this);
                return true;
            }

            instance = null;
            return false;
        }

        public IContainerScope CreateScope()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastContainer));

            throw new NotImplementedException("Scopes not yet implemented in FastContainer");
        }

        public bool IsRegistered<T>() where T : class
        {
            return _typeToHash.ContainsKey(typeof(T));
        }

        public bool IsRegistered(Type type)
        {
            return _typeToHash.ContainsKey(type);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var singleton in _singletons.Values)
            {
                if (singleton is IDisposable disposable)
                    disposable.Dispose();
            }

            _singletons.Clear();
            _factories.Clear();
            _typeToHash.Clear();
        }

        private void BuildFactories(Dictionary<Type, Registration> registrations)
        {
            foreach (var kvp in registrations)
            {
                var serviceType = kvp.Key;
                var registration = kvp.Value;
                var hash = GetTypeHash(serviceType);

                _typeToHash[serviceType] = hash;

                if (registration.Instance != null)
                {
                    _singletons[hash] = registration.Instance;
                    continue;
                }

                if (registration.Factory != null)
                {
                    _factories[hash] = registration.Factory;

                    if (registration.Lifetime == Lifetime.Singleton)
                    {
                        _singletonHashes.Add(hash);
                    }

                    continue;
                }

                var compiledFactory = CompileFactory(registration);
                _factories[hash] = compiledFactory;

                if (registration.Lifetime == Lifetime.Singleton)
                {
                    _singletonHashes.Add(hash);
                }
            }
        }

        private Func<IContainer, object> CompileFactory(Registration registration)
        {
            var implType = registration.ImplementationType;
            var constructor = FindBestConstructor(implType);
            var parameters = constructor.GetParameters();

            if (parameters.Length == 0)
            {
                return _ => Activator.CreateInstance(implType);
            }

            var containerParam = Expression.Parameter(typeof(IContainer), "container");
            var resolveMethod = typeof(IContainer).GetMethod(nameof(IContainer.Resolve), new[] { typeof(Type) });

            var argExpressions = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var resolveCall = Expression.Call(
                    containerParam,
                    resolveMethod,
                    Expression.Constant(paramType));
                argExpressions[i] = Expression.Convert(resolveCall, paramType);
            }

            var newExpression = Expression.New(constructor, argExpressions);
            var lambda = Expression.Lambda<Func<IContainer, object>>(
                Expression.Convert(newExpression, typeof(object)),
                containerParam);

            return lambda.Compile();
        }

        private ConstructorInfo FindBestConstructor(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            if (constructors.Length == 0)
                throw new InvalidOperationException($"No public constructor found for {type.Name}");

            ConstructorInfo best = constructors[0];
            foreach (var ctor in constructors)
            {
                if (ctor.GetParameters().Length > best.GetParameters().Length)
                    best = ctor;
            }

            return best;
        }

        private int GetTypeHash(Type type)
        {
            if (_typeToHash.TryGetValue(type, out var hash))
                return hash;

            var typeName = type.FullName ?? type.Name;
            return FNV1aHash(typeName);
        }

        private static int FNV1aHash(string text)
        {
            unchecked
            {
                const int fnvPrime = 16777619;
                int hash = (int)2166136261;

                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= fnvPrime;
                }

                return hash;
            }
        }
    }
}
