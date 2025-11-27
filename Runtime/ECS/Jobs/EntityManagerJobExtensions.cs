using System.Runtime.CompilerServices;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.Storage;
using Unity.Jobs;

namespace Strada.Core.ECS.Jobs
{
    public static class EntityManagerJobExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleParallel<TJob, T1>(
            this EntityManager manager,
            TJob job,
            int batchSize = EntityJobs.DefaultBatchSize,
            JobHandle dependency = default)
            where TJob : struct, IJobComponent<T1>
            where T1 : unmanaged, IComponent
        {
            var storage = manager.Store.GetOrCreateStorage<T1>();
            return EntityJobs.Schedule(job, storage, batchSize, dependency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleParallel<TJob, T1, T2>(
            this EntityManager manager,
            TJob job,
            int batchSize = EntityJobs.DefaultBatchSize,
            JobHandle dependency = default)
            where TJob : struct, IJobComponent<T1, T2>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            var s1 = manager.Store.GetOrCreateStorage<T1>();
            var s2 = manager.Store.GetOrCreateStorage<T2>();
            return EntityJobs.Schedule(job, s1, s2, batchSize, dependency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleParallel<TJob, T1, T2, T3>(
            this EntityManager manager,
            TJob job,
            int batchSize = EntityJobs.DefaultBatchSize,
            JobHandle dependency = default)
            where TJob : struct, IJobComponent<T1, T2, T3>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            var s1 = manager.Store.GetOrCreateStorage<T1>();
            var s2 = manager.Store.GetOrCreateStorage<T2>();
            var s3 = manager.Store.GetOrCreateStorage<T3>();
            return EntityJobs.Schedule(job, s1, s2, s3, batchSize, dependency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleParallel<TJob, T1, T2, T3, T4>(
            this EntityManager manager,
            TJob job,
            int batchSize = EntityJobs.DefaultBatchSize,
            JobHandle dependency = default)
            where TJob : struct, IJobComponent<T1, T2, T3, T4>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
        {
            var s1 = manager.Store.GetOrCreateStorage<T1>();
            var s2 = manager.Store.GetOrCreateStorage<T2>();
            var s3 = manager.Store.GetOrCreateStorage<T3>();
            var s4 = manager.Store.GetOrCreateStorage<T4>();
            return EntityJobs.Schedule(job, s1, s2, s3, s4, batchSize, dependency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunParallel<TJob, T1>(this EntityManager manager, TJob job)
            where TJob : struct, IJobComponent<T1>
            where T1 : unmanaged, IComponent
            => manager.ScheduleParallel<TJob, T1>(job).Complete();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunParallel<TJob, T1, T2>(this EntityManager manager, TJob job)
            where TJob : struct, IJobComponent<T1, T2>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            => manager.ScheduleParallel<TJob, T1, T2>(job).Complete();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunParallel<TJob, T1, T2, T3>(this EntityManager manager, TJob job)
            where TJob : struct, IJobComponent<T1, T2, T3>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            => manager.ScheduleParallel<TJob, T1, T2, T3>(job).Complete();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunParallel<TJob, T1, T2, T3, T4>(this EntityManager manager, TJob job)
            where TJob : struct, IJobComponent<T1, T2, T3, T4>
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
            => manager.ScheduleParallel<TJob, T1, T2, T3, T4>(job).Complete();
    }
}
