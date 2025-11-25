using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Jobs;
using Strada.Core.ECS.Query;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Strada.Core.Tests.Performance
{
    public struct JBenchPosition : IComponent { public float X, Y, Z; }
    public struct JBenchVelocity : IComponent { public float Vx, Vy, Vz; }
    public struct JBenchHealth : IComponent { public float Current, Max; }

    [BurstCompile]
    public struct JBenchMoveJob : IJobComponent<JBenchPosition, JBenchVelocity>
    {
        public float DeltaTime;

        [BurstCompile]
        public void Execute(int entity, ref JBenchPosition p, ref JBenchVelocity v)
        {
            p.X += v.Vx * DeltaTime;
            p.Y += v.Vy * DeltaTime;
            p.Z += v.Vz * DeltaTime;
        }
    }

    [TestFixture]
    [Category("Performance")]
    public class JobSystemPerformanceTests
    {
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
        }

        [TearDown]
        public void TearDown()
        {
            _entityManager?.Dispose();
        }

        [Test]
        public void Benchmark_100k_ParallelVsSequential_TargetSpeedup()
        {
            const int entityCount = 100_000;
            const int frames = 10;

            for (int i = 0; i < entityCount; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new JBenchPosition { X = i, Y = i, Z = i });
                _entityManager.AddComponent(entity, new JBenchVelocity { Vx = 1, Vy = 2, Vz = 3 });
            }

            var swSequential = Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++)
            {
                _entityManager.ForEach<JBenchPosition, JBenchVelocity>((int e, ref JBenchPosition p, ref JBenchVelocity v) =>
                {
                    p.X += v.Vx * 0.016f;
                    p.Y += v.Vy * 0.016f;
                    p.Z += v.Vz * 0.016f;
                });
            }
            swSequential.Stop();

            var job = new JBenchMoveJob { DeltaTime = 0.016f };
            _entityManager.ScheduleParallel<JBenchMoveJob, JBenchPosition, JBenchVelocity>(job).Complete();

            var swParallel = Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++)
            {
                _entityManager.RunParallel<JBenchMoveJob, JBenchPosition, JBenchVelocity>(job);
            }
            swParallel.Stop();

            float speedup = (float)swSequential.ElapsedMilliseconds / swParallel.ElapsedMilliseconds;

            UnityEngine.Debug.Log($"[JOB SYSTEM] 100k entities, {frames} frames:");
            UnityEngine.Debug.Log($"  Sequential: {swSequential.ElapsedMilliseconds}ms ({swSequential.ElapsedMilliseconds * 1000.0 / frames / entityCount * 1000:F0}ns/entity/frame)");
            UnityEngine.Debug.Log($"  Parallel:   {swParallel.ElapsedMilliseconds}ms ({swParallel.ElapsedMilliseconds * 1000.0 / frames / entityCount * 1000:F0}ns/entity/frame)");
            UnityEngine.Debug.Log($"  Speedup:    {speedup:F1}x");

            Assert.Greater(speedup, 1.5f, "Parallel should be at least 1.5x faster");
        }

        [Test]
        public void Benchmark_EntityCommandBuffer_10k_Commands()
        {
            ComponentPlayback.EnsureHandler<JBenchPosition>();

            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < 10; iter++)
            {
                var ecb = new EntityCommandBuffer(Allocator.TempJob, 64 * 1024);

                for (int i = 0; i < 10_000; i++)
                {
                    int entityIdx = ecb.CreateEntity();
                    ecb.AddComponent(entityIdx, new JBenchPosition { X = i, Y = i, Z = i });
                }

                ecb.Playback(_entityManager);
                ecb.Dispose();
            }
            sw.Stop();

            double msPerIteration = sw.ElapsedMilliseconds / 10.0;
            double nsPerCommand = sw.ElapsedTicks * 1_000_000_000.0 / Stopwatch.Frequency / 10 / 20_000;

            UnityEngine.Debug.Log($"[ECB] 10k create+add commands x 10 iterations:");
            UnityEngine.Debug.Log($"  Total: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Per iteration: {msPerIteration:F2}ms");
            UnityEngine.Debug.Log($"  Per command: {nsPerCommand:F0}ns");

            Assert.Less(msPerIteration, 50, "ECB playback should be under 50ms for 20k commands");

            _entityManager.Clear();
        }

        [Test]
        public void Benchmark_EntityCommandBuffer_Recording_Performance()
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob, 256 * 1024);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
            {
                ecb.CreateEntity();
            }
            sw.Stop();

            double nsPerCreate = sw.ElapsedTicks * 1_000_000_000.0 / Stopwatch.Frequency / 100_000;

            UnityEngine.Debug.Log($"[ECB Recording] 100k CreateEntity commands:");
            UnityEngine.Debug.Log($"  Total: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Per command: {nsPerCreate:F0}ns");

            Assert.Less(nsPerCreate, 500, "ECB recording should be under 500ns/command");

            ecb.Dispose();
        }

        [Test]
        public void Benchmark_JobChaining_100k()
        {
            const int entityCount = 100_000;

            for (int i = 0; i < entityCount; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new JBenchPosition { X = i, Y = i, Z = i });
                _entityManager.AddComponent(entity, new JBenchVelocity { Vx = 1, Vy = 2, Vz = 3 });
            }

            var moveJob = new JBenchMoveJob { DeltaTime = 0.016f };

            _entityManager.ScheduleParallel<JBenchMoveJob, JBenchPosition, JBenchVelocity>(moveJob).Complete();

            var sw = Stopwatch.StartNew();
            for (int frame = 0; frame < 10; frame++)
            {
                var h1 = _entityManager.ScheduleParallel<JBenchMoveJob, JBenchPosition, JBenchVelocity>(moveJob);
                var h2 = _entityManager.ScheduleParallel<JBenchMoveJob, JBenchPosition, JBenchVelocity>(moveJob, dependency: h1);
                var h3 = _entityManager.ScheduleParallel<JBenchMoveJob, JBenchPosition, JBenchVelocity>(moveJob, dependency: h2);
                h3.Complete();
            }
            sw.Stop();

            double msPerFrame = sw.ElapsedMilliseconds / 10.0;
            UnityEngine.Debug.Log($"[JOB CHAINING] 3 chained jobs x 10 frames ({entityCount} entities):");
            UnityEngine.Debug.Log($"  Total: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Per frame: {msPerFrame:F2}ms");

            Assert.Less(msPerFrame, 20, "3 chained jobs should complete in under 20ms per frame");
        }
    }
}
