using System;
using System.Collections.Generic;
using Strada.Core.DI;

namespace Strada.Core.Module
{
    public enum ModulePhase
    {
        None = 0,
        PreInitialize = 1,
        Initialize = 2,
        PostInitialize = 3,
        Ready = 4,
        Shutdown = 5,
        Disposed = 6
    }

    public interface IModuleLifecycle
    {
        ModulePhase Phase { get; }
        void PreInitialize(IContainer container);
        void Initialize(IContainer container);
        void PostInitialize(IContainer container);
        void Shutdown();
    }

    public abstract class LifecycleModule : IModule, IModuleLifecycle, IDisposable
    {
        private ModulePhase _phase = ModulePhase.None;
        private ModuleScope _scope;
        private bool _disposed;

        public ModulePhase Phase => _phase;
        protected ModuleScope Scope => _scope;
        protected IContainer Container { get; private set; }

        public abstract string Name { get; }
        public virtual int Priority => 0;
        public virtual IEnumerable<Type> Dependencies => Array.Empty<Type>();

        // IModule implementation
        public virtual void RegisterServices(ContainerBuilder builder) { }
        public virtual IEnumerable<Type> GetSystemTypes() => Array.Empty<Type>();

        // IModule.Initialize is mapped to our lifecycle-aware version
        void IModule.Initialize(IContainer container)
        {
            // For modules that don't use the full lifecycle, allow direct initialize
            if (_phase == ModulePhase.None)
            {
                PreInitialize(container);
            }
            Initialize(container);
            PostInitialize(container);
        }

        public void PreInitialize(IContainer container)
        {
            if (_phase != ModulePhase.None) return;

            Container = container;
            _scope = new ModuleScope(container);
            _phase = ModulePhase.PreInitialize;

            OnPreInitialize();
        }

        public void Initialize(IContainer container)
        {
            if (_phase != ModulePhase.PreInitialize) return;

            _phase = ModulePhase.Initialize;
            OnInitialize();
        }

        public void PostInitialize(IContainer container)
        {
            if (_phase != ModulePhase.Initialize) return;

            _phase = ModulePhase.PostInitialize;
            OnPostInitialize();
            _phase = ModulePhase.Ready;
        }

        public void Shutdown()
        {
            if (_phase == ModulePhase.Shutdown || _phase == ModulePhase.Disposed) return;

            _phase = ModulePhase.Shutdown;
            OnShutdown();
        }

        protected virtual void OnPreInitialize() { }
        protected virtual void OnInitialize() { }
        protected virtual void OnPostInitialize() { }
        protected virtual void OnShutdown() { }
        protected virtual void OnDispose() { }

        protected void RegisterLocal<T>(T instance) where T : class
        {
            _scope?.RegisterInstance(instance);
        }

        protected void RegisterLocalFactory<T>(Func<T> factory) where T : class
        {
            _scope?.RegisterFactory(factory);
        }

        protected T Resolve<T>() where T : class
        {
            return _scope?.Resolve<T>() ?? Container?.Resolve<T>();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Shutdown();
            OnDispose();
            _scope?.Dispose();
            _phase = ModulePhase.Disposed;

            GC.SuppressFinalize(this);
        }
    }
}
