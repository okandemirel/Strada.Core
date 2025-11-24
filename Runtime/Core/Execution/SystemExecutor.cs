using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.ECS;

namespace Strada.Core.Execution
{
    public sealed class SystemExecutor : IDisposable
    {
        private readonly List<IStradaSystem> _systems;
        private readonly EntityManager _entityManager;
        private readonly IContainer _container;
        private SystemState _state;
        private double _time;
        private bool _disposed;

        public SystemExecutor(List<IStradaSystem> systems, EntityManager entityManager, IContainer container)
        {
            _systems = systems;
            _entityManager = entityManager;
            _container = container;
            _state = new SystemState
            {
                EntityManager = entityManager,
                Enabled = true
            };
        }

        public void Initialize()
        {
            foreach (var system in _systems)
            {
                system.OnCreate(ref _state);
            }
        }

        public void Update(float deltaTime)
        {
            _time += deltaTime;
            _state.DeltaTime = deltaTime;
            _state.Time = _time;

            foreach (var system in _systems)
            {
                if (_state.Enabled)
                {
                    system.OnUpdate(ref _state);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            for (int i = _systems.Count - 1; i >= 0; i--)
            {
                _systems[i].OnDestroy(ref _state);
            }

            _disposed = true;
        }
    }
}
