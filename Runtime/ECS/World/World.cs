using System;
using Strada.Core.Signals;

namespace Strada.Core.ECS
{
    public sealed class World : IDisposable
    {
        private readonly EntityManager _entities;
        private readonly SystemScheduler _scheduler;
        private readonly SignalBus _signals;
        private bool _initialized;
        private bool _disposed;

        public EntityManager Entities => _entities;
        public SignalBus Signals => _signals;
        public bool IsInitialized => _initialized;

        internal World(EntityManager entities, SystemScheduler scheduler, SignalBus signals)
        {
            _entities = entities;
            _scheduler = scheduler;
            _signals = signals;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _scheduler.Initialize();
        }

        public void Update(float deltaTime)
        {
            _scheduler.Update(deltaTime);
        }

        public void LateUpdate(float deltaTime)
        {
            _scheduler.LateUpdate(deltaTime);
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            _scheduler.FixedUpdate(fixedDeltaTime);
        }

        public Entity CreateEntity() => _entities.CreateEntity();

        public void DestroyEntity(Entity entity) => _entities.DestroyEntity(entity);

        public void AddComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
            => _entities.AddComponent(entity, component);

        public T GetComponent<T>(Entity entity) where T : unmanaged, IComponent
            => _entities.GetComponent<T>(entity);

        public void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
            => _entities.SetComponent(entity, component);

        public bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent
            => _entities.HasComponent<T>(entity);

        public void RemoveComponent<T>(Entity entity) where T : unmanaged, IComponent
            => _entities.RemoveComponent<T>(entity);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _scheduler.Dispose();
            _entities.Dispose();
            _signals.Dispose();
        }
    }
}
