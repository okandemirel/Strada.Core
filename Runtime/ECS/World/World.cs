using System;
using Strada.Core.Communication;

namespace Strada.Core.ECS
{
    public sealed class World : IDisposable
    {
        private static World _current;

        private readonly EntityManager _entities;
        private readonly SystemScheduler _scheduler;
        private readonly MessageBus _bus;
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

        /// <summary>
        /// Gets the EntityManager responsible for creating, destroying, and managing entities.
        /// </summary>
        public EntityManager EntityManager => _entities;

        /// <summary>
        /// Gets the SystemScheduler responsible for executing systems in the correct order.
        /// </summary>
        public SystemScheduler SystemScheduler => _scheduler;

        /// <summary>
        /// Gets the MessageBus for publish/subscribe communication.
        /// </summary>
        public MessageBus MessageBus => _bus;

        /// <summary>
        /// Gets a value indicating whether the World has been initialized.
        /// </summary>
        public bool IsInitialized => _initialized;

        internal World(EntityManager entities, SystemScheduler scheduler, MessageBus bus)
        {
            _entities = entities;
            _scheduler = scheduler;
            _bus = bus;
        }

        /// <summary>
        /// Initializes the World and its subsystems.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _scheduler.Initialize();
        }

        /// <summary>
        /// Updates the World's systems (Variable Time Step).
        /// </summary>
        /// <param name="deltaTime">Time since last frame.</param>
        public void Update(float deltaTime)
        {
            _scheduler.Update(deltaTime);
        }

        /// <summary>
        /// Updates the World's systems (Late Update).
        /// </summary>
        /// <param name="deltaTime">Time since last frame.</param>
        public void LateUpdate(float deltaTime)
        {
            _scheduler.LateUpdate(deltaTime);
        }

        /// <summary>
        /// Updates the World's systems (Fixed Time Step).
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed time step duration.</param>
        public void FixedUpdate(float fixedDeltaTime)
        {
            _scheduler.FixedUpdate(fixedDeltaTime);
        }

        /// <summary>
        /// Creates a new Entity in this World.
        /// </summary>
        public Entity CreateEntity() => _entities.CreateEntity();

        /// <summary>
        /// Destroys an Entity and recycles its ID.
        /// </summary>
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
