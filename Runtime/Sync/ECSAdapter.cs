using System;
using Strada.Core.Communication;
using Strada.Core.Core;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.World;
using Strada.Core.Patterns.Interfaces;

namespace Strada.Core.Sync
{
    public class ECSAdapter : IService, ILoopRunner, IDisposable
    {
        private World _world;
        private bool _autoUpdate = true;
        private bool _registeredWithLoop;

        public World World => _world;
        public EntityManager EntityManager => _world?.EntityManager;
        public MessageBus MessageBus => _world?.MessageBus;
        public bool AutoUpdate { get => _autoUpdate; set => _autoUpdate = value; }

        public ECSAdapter() { }

        public ECSAdapter(World world)
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
            RegisterWithPlayerLoop();
        }

        public void RegisterWithPlayerLoop()
        {
            if (_registeredWithLoop) return;
            _registeredWithLoop = true;

            PlayerLoop.RegisterUpdate(OnUpdate);
            PlayerLoop.RegisterLateUpdate(OnLateUpdate);
            PlayerLoop.RegisterFixedUpdate(OnFixedUpdate);
        }

        public void UnregisterFromPlayerLoop()
        {
            if (!_registeredWithLoop) return;
            _registeredWithLoop = false;

            PlayerLoop.UnregisterUpdate(OnUpdate);
            PlayerLoop.UnregisterLateUpdate(OnLateUpdate);
            PlayerLoop.UnregisterFixedUpdate(OnFixedUpdate);
        }

        public void Update(float deltaTime) => OnUpdate(deltaTime);
        public void LateUpdate(float deltaTime) => OnLateUpdate(deltaTime);
        public void FixedUpdate(float fixedDeltaTime) => OnFixedUpdate(fixedDeltaTime);

        public void OnUpdate(float deltaTime)
        {
            if (_autoUpdate)
                _world?.Update(deltaTime);
        }

        public void OnLateUpdate(float deltaTime)
        {
            if (_autoUpdate)
                _world?.LateUpdate(deltaTime);
        }

        public void OnFixedUpdate(float fixedDeltaTime)
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
            UnregisterFromPlayerLoop();
            _world?.Dispose();
            _world = null;
        }
    }
}
