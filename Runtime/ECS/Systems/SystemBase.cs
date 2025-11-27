using System;
using System.Runtime.CompilerServices;
using Strada.Core.Communication;
using Strada.Core.DI.Attributes;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.Query;
using Strada.Core.ECS.Storage;

namespace Strada.Core.ECS.Systems
{
    public abstract class SystemBase : ISystem
    {
        private bool _initialized;
        private bool _disposed;

        protected EntityManager EntityManager { get; private set; }
        protected MessageBus MessageBus { get; private set; }

        public void Inject(EntityManager entityManager, MessageBus bus = null)
        {
            EntityManager = entityManager;
            MessageBus = bus;
        }

        public void Initialize()
        {
            if (_initialized) return;
            OnInitialize();
            _initialized = true;
        }

        public void Update(float deltaTime)
        {
            if (!_initialized || _disposed) return;
            OnUpdate(deltaTime);
        }

        public void Dispose()
        {
            if (_disposed) return;
            OnDispose();
            _disposed = true;
        }

        protected virtual void OnInitialize() { }
        protected abstract void OnUpdate(float deltaTime);
        protected virtual void OnDispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected QueryBuilder Query() => EntityManager.Query();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ForEach<T1>(QueryDelegate<T1> action) where T1 : unmanaged, IComponent
        {
            EntityManager.ForEach(action);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ForEach<T1, T2>(QueryDelegate<T1, T2> action)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            EntityManager.ForEach(action);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ForEach<T1, T2, T3>(QueryDelegate<T1, T2, T3> action)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            EntityManager.ForEach(action);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Entity CreateEntity() => EntityManager.CreateEntity();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void DestroyEntity(Entity entity) => EntityManager.DestroyEntity(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Publish<T>(T evt) where T : struct
        {
            MessageBus?.Publish(evt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Send<T>(T command) where T : struct
        {
            MessageBus?.Send(command);
        }
    }

    public abstract class SystemBase<T1> : SystemBase
        where T1 : unmanaged, IComponent
    {
        protected sealed override void OnUpdate(float deltaTime)
        {
            ForEach<T1>((int entity, ref T1 c1) => OnUpdateEntity(entity, ref c1, deltaTime));
        }

        protected abstract void OnUpdateEntity(int entityIndex, ref T1 c1, float deltaTime);
    }

    public abstract class SystemBase<T1, T2> : SystemBase
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        protected sealed override void OnUpdate(float deltaTime)
        {
            ForEach<T1, T2>((int entity, ref T1 c1, ref T2 c2) => OnUpdateEntity(entity, ref c1, ref c2, deltaTime));
        }

        protected abstract void OnUpdateEntity(int entityIndex, ref T1 c1, ref T2 c2, float deltaTime);
    }

    public abstract class SystemBase<T1, T2, T3> : SystemBase
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        protected sealed override void OnUpdate(float deltaTime)
        {
            ForEach<T1, T2, T3>((int entity, ref T1 c1, ref T2 c2, ref T3 c3) =>
                OnUpdateEntity(entity, ref c1, ref c2, ref c3, deltaTime));
        }

        protected abstract void OnUpdateEntity(int entityIndex, ref T1 c1, ref T2 c2, ref T3 c3, float deltaTime);
    }
}
