using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.Communication;
using Strada.Core.Module;
using Strada.Core.Execution;
using Strada.Core.Data.UnityObjects;

namespace Strada.Core
{
    public sealed class World : IDisposable
    {
        private readonly FastContainer _container;
        private readonly EntityManager _entityManager;
        private readonly MessageBus _messageBus;
        private readonly Execution.SystemExecutor _systemExecutor;
        private readonly List<IModule> _modules;
        private bool _initialized;
        private bool _disposed;

        public IContainer Container => _container;
        public EntityManager EntityManager => _entityManager;
        public MessageBus MessageBus => _messageBus;
        public bool IsInitialized => _initialized;

        public static World Current { get; private set; }

        private World(FastContainer container, EntityManager entityManager, MessageBus messageBus, Execution.SystemExecutor systemExecutor, List<IModule> modules)
        {
            _container = container;
            _entityManager = entityManager;
            _messageBus = messageBus;
            _systemExecutor = systemExecutor;
            _modules = modules;
        }

        public static World Create(CD_World worldConfig)
        {
            var entityManager = new EntityManager();
            var messageBus = new MessageBus();
            var modules = new List<IModule>();

            var builder = new ContainerBuilder();
            builder.UseFastContainer();

            builder.RegisterInstance<EntityManager>(entityManager);
            builder.RegisterInstance<MessageBus>(messageBus);

            foreach (var moduleConfig in worldConfig.Config.Modules)
            {
                if (!moduleConfig.Enabled) continue;

                var module = CreateModule(moduleConfig.TypeName);
                module.RegisterServices(builder);
                module.RegisterSystems(entityManager);
                modules.Add(module);
            }

            var container = (FastContainer)builder.Build();

            var systems = new List<IStradaSystem>();
            foreach (var module in modules)
            {
                systems.AddRange(module.GetSystems());
            }

            var systemExecutor = new Execution.SystemExecutor(systems, entityManager, container);

            var world = new World(container, entityManager, messageBus, systemExecutor, modules);
            Current = world;
            return world;
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

            _messageBus.ProcessCommands(_entityManager);
            _systemExecutor.Update(deltaTime);
            _messageBus.DispatchEvents();
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
            return (IModule)Activator.CreateInstance(type);
        }
    }
}
