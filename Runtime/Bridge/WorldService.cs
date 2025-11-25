using System;
using Strada.Core.ECS;
using Strada.Core.MVCS.Interfaces;
using Strada.Core.Signals;

namespace Strada.Core.Bridge
{
    public class WorldService : IService, IDisposable
    {
        private World _world;
        private bool _autoUpdate = true;

        public World World => _world;
        public EntityManager Entities => _world?.Entities;
        public SignalBus Signals => _world?.Signals;
        public bool AutoUpdate { get => _autoUpdate; set => _autoUpdate = value; }

        public WorldService() { }

        public WorldService(World world)
        {
            _world = world;
        }

        public void SetWorld(World world)
        {
            _world?.Dispose();
            _world = world;
        }

        public void Initialize()
        {
            _world?.Initialize();
        }

        public void Update(float deltaTime)
        {
            if (_autoUpdate)
                _world?.Update(deltaTime);
        }

        public void LateUpdate(float deltaTime)
        {
            if (_autoUpdate)
                _world?.LateUpdate(deltaTime);
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            if (_autoUpdate)
                _world?.FixedUpdate(fixedDeltaTime);
        }

        public Entity CreateEntity() => _world.CreateEntity();

        public void DestroyEntity(Entity entity) => _world.DestroyEntity(entity);

        public void AddComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
            => _world.AddComponent(entity, component);

        public T GetComponent<T>(Entity entity) where T : unmanaged, IComponent
            => _world.GetComponent<T>(entity);

        public void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
            => _world.SetComponent(entity, component);

        public bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent
            => _world.HasComponent<T>(entity);

        public void Dispose()
        {
            _world?.Dispose();
            _world = null;
        }
    }
}
