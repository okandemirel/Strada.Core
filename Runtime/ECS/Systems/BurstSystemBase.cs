using System.Runtime.CompilerServices;
using Strada.Core.ECS.Jobs;
using Strada.Core.ECS.Storage;
using Unity.Burst;
using Unity.Jobs;

namespace Strada.Core.ECS.Systems
{
    public abstract class BurstSystem<TJob, T1> : JobSystemBase
        where TJob : struct, IJobComponent<T1>
        where T1 : unmanaged, IComponent
    {
        private ComponentStorage<T1> _storage;
        protected int BatchSize { get; set; } = EntityJobs.DefaultBatchSize;

        protected override void OnCreate()
        {
            _storage = EntityManager.Store.GetOrCreateStorage<T1>();
        }

        protected sealed override JobHandle OnSchedule(float deltaTime, JobHandle dependency)
        {
            var job = CreateJob(deltaTime);
            return EntityJobs.Schedule(job, _storage, BatchSize, dependency);
        }

        protected abstract TJob CreateJob(float deltaTime);
    }

    public abstract class BurstSystem<TJob, T1, T2> : JobSystemBase
        where TJob : struct, IJobComponent<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        private ComponentStorage<T1> _storage1;
        private ComponentStorage<T2> _storage2;
        protected int BatchSize { get; set; } = EntityJobs.DefaultBatchSize;

        protected override void OnCreate()
        {
            _storage1 = EntityManager.Store.GetOrCreateStorage<T1>();
            _storage2 = EntityManager.Store.GetOrCreateStorage<T2>();
        }

        protected sealed override JobHandle OnSchedule(float deltaTime, JobHandle dependency)
        {
            var job = CreateJob(deltaTime);
            return EntityJobs.Schedule(job, _storage1, _storage2, BatchSize, dependency);
        }

        protected abstract TJob CreateJob(float deltaTime);
    }

    public abstract class BurstSystem<TJob, T1, T2, T3> : JobSystemBase
        where TJob : struct, IJobComponent<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        private ComponentStorage<T1> _storage1;
        private ComponentStorage<T2> _storage2;
        private ComponentStorage<T3> _storage3;
        protected int BatchSize { get; set; } = EntityJobs.DefaultBatchSize;

        protected override void OnCreate()
        {
            _storage1 = EntityManager.Store.GetOrCreateStorage<T1>();
            _storage2 = EntityManager.Store.GetOrCreateStorage<T2>();
            _storage3 = EntityManager.Store.GetOrCreateStorage<T3>();
        }

        protected sealed override JobHandle OnSchedule(float deltaTime, JobHandle dependency)
        {
            var job = CreateJob(deltaTime);
            return EntityJobs.Schedule(job, _storage1, _storage2, _storage3, BatchSize, dependency);
        }

        protected abstract TJob CreateJob(float deltaTime);
    }

    public abstract class BurstSystem<TJob, T1, T2, T3, T4> : JobSystemBase
        where TJob : struct, IJobComponent<T1, T2, T3, T4>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        private ComponentStorage<T1> _storage1;
        private ComponentStorage<T2> _storage2;
        private ComponentStorage<T3> _storage3;
        private ComponentStorage<T4> _storage4;
        protected int BatchSize { get; set; } = EntityJobs.DefaultBatchSize;

        protected override void OnCreate()
        {
            _storage1 = EntityManager.Store.GetOrCreateStorage<T1>();
            _storage2 = EntityManager.Store.GetOrCreateStorage<T2>();
            _storage3 = EntityManager.Store.GetOrCreateStorage<T3>();
            _storage4 = EntityManager.Store.GetOrCreateStorage<T4>();
        }

        protected sealed override JobHandle OnSchedule(float deltaTime, JobHandle dependency)
        {
            var job = CreateJob(deltaTime);
            return EntityJobs.Schedule(job, _storage1, _storage2, _storage3, _storage4, BatchSize, dependency);
        }

        protected abstract TJob CreateJob(float deltaTime);
    }
}
