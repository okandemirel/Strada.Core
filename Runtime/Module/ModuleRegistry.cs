using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.DI;

namespace Strada.Core.Module
{
    public sealed class ModuleRegistry : IDisposable
    {
        private readonly List<IModule> _modules = new(16);
        private readonly Dictionary<Type, IModule> _modulesByType = new(16);
        private bool _initialized;
        private bool _disposed;

        public int ModuleCount => _modules.Count;
        public IReadOnlyList<IModule> Modules => _modules;
        public bool IsInitialized => _initialized;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Register<T>(T module) where T : class, IModule
        {
            if (_initialized)
                return;

            _modules.Add(module);
            _modulesByType[typeof(T)] = module;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>() where T : class, IModule
        {
            return _modulesByType.TryGetValue(typeof(T), out var module) ? (T)module : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(out T module) where T : class, IModule
        {
            if (_modulesByType.TryGetValue(typeof(T), out var m))
            {
                module = (T)m;
                return true;
            }
            module = null;
            return false;
        }

        public void RegisterAllServices(ContainerBuilder builder)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].RegisterServices(builder);
            }
        }

        public IEnumerable<Type> GetAllSystemTypes()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                foreach (var type in _modules[i].GetSystemTypes())
                {
                    yield return type;
                }
            }
        }

        public void InitializeAll(IContainer container)
        {
            if (_initialized) return;

            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Initialize(container);
            }
            _initialized = true;
        }

        public void ShutdownAll()
        {
            if (_disposed) return;

            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i].Shutdown();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            ShutdownAll();
            _modules.Clear();
            _modulesByType.Clear();
        }
    }
}
