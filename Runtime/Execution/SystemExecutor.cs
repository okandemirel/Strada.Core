using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.ECS;

namespace Strada.Core.Execution
{
    /// <summary>
    /// Manages the lifecycle and execution of all registered ECS systems.
    /// It resolves systems from the DI container, enabling automatic dependency injection.
    /// </summary>
    public sealed class SystemExecutor : IDisposable
    {
        private readonly List<ISystem> _systems = new List<ISystem>();
        private readonly IContainer _container;

        public SystemExecutor(List<Type> systemTypes, EntityManager entityManager, IContainer container)
        {
            _container = container;

            foreach (var systemType in systemTypes)
            {
                if (!typeof(ISystem).IsAssignableFrom(systemType))
                {
                    throw new ArgumentException($"Type {systemType.FullName} does not implement ISystem and cannot be added to the SystemExecutor.");
                }

                // The container will create the system and inject its dependencies.
                var system = (ISystem)container.Resolve(systemType);
                _systems.Add(system);
            }
        }

        public void Initialize()
        {
            foreach (var system in _systems)
            {
                system.Initialize();
            }
        }

        public void Update(float deltaTime)
        {
            foreach (var system in _systems)
            {
                system.Update(deltaTime);
            }
        }

        public void Dispose()
        {
            for (int i = _systems.Count - 1; i >= 0; i--)
            {
                _systems[i].Dispose();
            }
            _systems.Clear();
        }
    }
}