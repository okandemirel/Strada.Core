using System;
using System.Collections.Generic;
using Strada.Core.Communication;
using Strada.Core.Core.Data.UnityObjects;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.Execution;
using Strada.Core.Module;

namespace Strada.Core.Core
{
    public sealed class World : IDisposable
    {
        private readonly IContainer _container;
        private readonly EntityManager _entityManager;
        private readonly MessageBus _messageBus;
        private readonly SystemExecutor _systemExecutor;
        private readonly List<IModule> _modules;
        private bool _initialized;
        private bool _disposed;

        public IContainer Container => _container;
        public EntityManager EntityManager => _entityManager;
        public MessageBus MessageBus => _messageBus;
        public bool IsInitialized => _initialized;

        public static World Current { get; private set; }

        private World(IContainer container, EntityManager entityManager, MessageBus messageBus, SystemExecutor systemExecutor, List<IModule> modules)
        {
            _container = container;
            _entityManager = entityManager;
            _messageBus = messageBus;
            _systemExecutor = systemExecutor;
            _modules = modules;
        }

        public static World Create(CD_World world)
        {
            var definition = world.Definition;
            var entityManager = new EntityManager();
            var messageBus = new MessageBus();
            var modules = new List<IModule>();

            var builder = new ContainerBuilder();

            builder.RegisterInstance(entityManager);
            builder.RegisterInstance(messageBus);

            foreach (var moduleConfig in definition.Modules)
            {
                if (!moduleConfig.Enabled) continue;

                var module = CreateModule(moduleConfig.TypeName);
                module.RegisterServices(builder);
                modules.Add(module);
            }

            var container = builder.Build();

            var systemTypes = new List<Type>();
            foreach (var module in modules)
            {
                systemTypes.AddRange(module.GetSystemTypes());
            }

            var systemExecutor = new SystemExecutor(systemTypes, entityManager, container);

            var worldInstance = new World(container, entityManager, messageBus, systemExecutor, modules);
            Current = worldInstance;
            return worldInstance;
        }

        public void Initialize()
        {
            if (_initialized)
                return;

            foreach (var module in _modules)
            {
                module.Initialize(_container);
            }

            _systemExecutor.Initialize();
            _initialized = true;
        }

        public void Update(float deltaTime)
        {
            if (!_initialized || _disposed)
                return;

            _systemExecutor.Update(deltaTime);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i].Shutdown();
            }

            _systemExecutor?.Dispose();
            _entityManager?.Dispose();
            _messageBus?.Dispose();
            _container?.Dispose();

            if (Current == this)
                Current = null;

            _disposed = true;
        }

        private static IModule CreateModule(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null)
            {
                throw new InvalidOperationException($"[World] Module type '{typeName}' could not be found.");
            }
            return (IModule)Activator.CreateInstance(type);
        }
    }
}
