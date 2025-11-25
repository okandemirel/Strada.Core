using System.Runtime.CompilerServices;
using Strada.Core.ECS.Jobs;
using Strada.Core.ECS.Storage;
using Unity.Collections;
using Unity.Jobs;

namespace Strada.Core.ECS.Systems
{
    public abstract class JobSystemBase : SystemBase
    {
        private JobHandle _lastJobHandle;
        private EntityCommandBuffer _commandBuffer;
        private bool _commandBufferCreated;

        protected JobHandle Dependency
        {
            get => _lastJobHandle;
            set => _lastJobHandle = value;
        }

        protected EntityCommandBuffer CommandBuffer
        {
            get
            {
                if (!_commandBufferCreated)
                {
                    _commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
                    _commandBufferCreated = true;
                }
                return _commandBuffer;
            }
        }

        protected override void OnInitialize() => OnCreate();
        protected override void OnDispose() => OnDestroy();

        protected virtual void OnCreate() { }
        protected virtual void OnDestroy() { }

        protected sealed override void OnUpdate(float deltaTime)
        {
            _lastJobHandle.Complete();

            if (_commandBufferCreated)
            {
                _commandBuffer.Playback(EntityManager);
                _commandBuffer.Clear();
            }

            _lastJobHandle = OnSchedule(deltaTime, _lastJobHandle);
        }

        protected abstract JobHandle OnSchedule(float deltaTime, JobHandle dependency);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected JobHandle ScheduleParallel<TJob, T1>(
            TJob job,
            int batchSize = EntityJobs.DefaultBatchSize,
            JobHandle dependency = default)
            where TJob : struct, IJobComponent<T1>
            where T1 : unmanaged, IComponent
        {
            return EntityManager.ScheduleParallel<TJob, T1>(job, batchSize, dependency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected JobHandle ScheduleParallel<TJob, T1, T2>(
            TJob job,
            int batchSize = EntityJobs.DefaultBatchSize,
            JobHandle dependency = default)
            where TJob : struct, IJobComponent<T1, T2>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return EntityManager.ScheduleParallel<TJob, T1, T2>(job, batchSize, dependency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected JobHandle ScheduleParallel<TJob, T1, T2, T3>(
            TJob job,
            int batchSize = EntityJobs.DefaultBatchSize,
            JobHandle dependency = default)
            where TJob : struct, IJobComponent<T1, T2, T3>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return EntityManager.ScheduleParallel<TJob, T1, T2, T3>(job, batchSize, dependency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected JobHandle ScheduleParallel<TJob, T1, T2, T3, T4>(
            TJob job,
            int batchSize = EntityJobs.DefaultBatchSize,
            JobHandle dependency = default)
            where TJob : struct, IJobComponent<T1, T2, T3, T4>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
        {
            return EntityManager.ScheduleParallel<TJob, T1, T2, T3, T4>(job, batchSize, dependency);
        }

        public void CompleteAllJobs()
        {
            _lastJobHandle.Complete();
            _lastJobHandle = default;
        }

        public void FlushCommandBuffer()
        {
            if (_commandBufferCreated)
            {
                _commandBuffer.Playback(EntityManager);
                _commandBuffer.Clear();
            }
        }
    }
}
