using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.ECS;

namespace Strada.Core.Module
{
    public abstract class ModuleBase : IModule
    {
        private bool _initialized;
        private bool _disposed;

        protected IContainer Container { get; private set; }

        public bool IsInitialized => _initialized;

        public abstract void RegisterServices(ContainerBuilder builder);

        public virtual IEnumerable<Type> GetSystemTypes() => Array.Empty<Type>();

        public void Initialize(IContainer container)
        {
            if (_initialized) return;

            Container = container;
            OnInitialize();
            _initialized = true;
        }

        public void Shutdown()
        {
            if (_disposed) return;

            OnShutdown();
            Container = null;
            _disposed = true;
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnShutdown() { }

        protected T Resolve<T>() where T : class
        {
            return Container?.Resolve<T>();
        }

        protected bool TryResolve<T>(out T instance) where T : class
        {
            if (Container == null)
            {
                instance = null;
                return false;
            }
            return Container.TryResolve(out instance);
        }
    }

    public abstract class ModuleBase<TConfig> : ModuleBase where TConfig : class
    {
        protected TConfig Config { get; private set; }

        public void SetConfig(TConfig config)
        {
            Config = config;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (Config == null && TryResolve<TConfig>(out var config))
            {
                Config = config;
            }
        }
    }
}
