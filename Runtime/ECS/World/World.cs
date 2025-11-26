using System;
using Strada.Core.Communication;

namespace Strada.Core.ECS
{
    public sealed class World : IDisposable
    {
        private static World _current;

        private readonly EntityManager _entities;
        private readonly SystemScheduler _scheduler;
        private readonly StradaBus _bus;
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the current active World instance.
        /// Used by editor tools and debugging utilities.
        /// </summary>
        public static World Current
        {
            get => _current;
            set => _current = value;
        }

        public EntityManager Entities => _entities;
        public StradaBus Bus => _bus;
        public bool IsInitialized => _initialized;

        internal World(EntityManager entities, SystemScheduler scheduler, StradaBus bus)
        {
            _entities = entities;
            _scheduler = scheduler;
            _bus = bus;
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

            if (_current == this)
                _current = null;

            _scheduler.Dispose();
            _entities.Dispose();
            _bus?.Dispose();
        }
    }
}
