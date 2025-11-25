using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.DI;

namespace Strada.Core.Module
{
    public sealed class ModuleScope : IContainerScope
    {
        private readonly IContainer _parent;
        private readonly Dictionary<Type, object> _localInstances;
        private readonly Dictionary<Type, Func<object>> _localFactories;
        private readonly List<IDisposable> _disposables;
        private bool _disposed;

        public IContainer Parent => _parent;

        public ModuleScope(IContainer parent)
        {
            _parent = parent;
            _localInstances = new Dictionary<Type, object>(16);
            _localFactories = new Dictionary<Type, Func<object>>(16);
            _disposables = new List<IDisposable>(8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Resolve<T>() where T : class
        {
            var type = typeof(T);

            if (_localInstances.TryGetValue(type, out var instance))
                return (T)instance;

            if (_localFactories.TryGetValue(type, out var factory))
            {
                var created = (T)factory();
                _localInstances[type] = created;
                return created;
            }

            return _parent.Resolve<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object Resolve(Type type)
        {
            if (_localInstances.TryGetValue(type, out var instance))
                return instance;

            if (_localFactories.TryGetValue(type, out var factory))
            {
                var created = factory();
                _localInstances[type] = created;
                return created;
            }

            return _parent.Resolve(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolve<T>(out T instance) where T : class
        {
            var type = typeof(T);

            if (_localInstances.TryGetValue(type, out var cached))
            {
                instance = (T)cached;
                return true;
            }

            if (_localFactories.TryGetValue(type, out var factory))
            {
                instance = (T)factory();
                _localInstances[type] = instance;
                return true;
            }

            return _parent.TryResolve(out instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRegistered<T>() where T : class
        {
            var type = typeof(T);
            return _localInstances.ContainsKey(type) ||
                   _localFactories.ContainsKey(type) ||
                   _parent.IsRegistered<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRegistered(Type type)
        {
            return _localInstances.ContainsKey(type) ||
                   _localFactories.ContainsKey(type) ||
                   _parent.IsRegistered(type);
        }

        public IContainerScope CreateScope()
        {
            return new ModuleScope(this);
        }

        public void RegisterInstance<T>(T instance) where T : class
        {
            _localInstances[typeof(T)] = instance;

            if (instance is IDisposable disposable)
                _disposables.Add(disposable);
        }

        public void RegisterFactory<T>(Func<T> factory) where T : class
        {
            _localFactories[typeof(T)] = () => factory();
        }

        public void RegisterFactory<TInterface, TImplementation>(Func<TImplementation> factory)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            _localFactories[typeof(TInterface)] = () => factory();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var disposable in _disposables)
                disposable.Dispose();

            _disposables.Clear();
            _localInstances.Clear();
            _localFactories.Clear();
        }
    }
}
