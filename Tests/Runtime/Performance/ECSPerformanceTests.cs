using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Query;
using Strada.Core.ECS.Storage;
using Strada.Core.ECS.Groups;

namespace Strada.Core.Tests.Performance
{
    public struct Transform : IComponent
    {
        public float X, Y, Z;
        public float RotX, RotY, RotZ, RotW;
        public float ScaleX, ScaleY, ScaleZ;
    }

    public struct Velocity : IComponent
    {
        public float X, Y, Z;
    }

    public struct Health : IComponent
    {
        public int Current;
        public int Max;
    }

    public struct AliveState { }
    public struct DeadState { }

    [TestFixture]
    [Category("Performance")]
    public class ECSPerformanceTests
    {
        private EntityManager _entityManager;

        [SetUp]
        public void Setup()
        {
            _entityManager = new EntityManager();
        }

        [TearDown]
        public void TearDown()
        {
            _entityManager?.Dispose();
        }

        [Test]
        public void Benchmark_EntityCreation_100k()
        {
            const int count = 100000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _entityManager.CreateEntity();
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] Create {count} entities: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1000 / count:F2}μs per entity");

            Assert.Less(sw.ElapsedMilliseconds, 100, "Entity creation too slow");
            Assert.AreEqual(count, _entityManager.EntityCount);
        }

        [Test]
        public void Benchmark_EntityWithComponent_100k()
        {
            const int count = 100000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Transform { X = i, Y = i, Z = i });
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] Create {count} entities with component: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1000 / count:F2}μs per entity");

            Assert.Less(sw.ElapsedMilliseconds, 200, "Entity+component creation too slow");
        }

        [Test]
        public void Benchmark_SingleComponentQuery_100k()
        {
            const int count = 100000;

            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Transform { X = i, Y = i, Z = i });
            }

            int iterationCount = 0;
            float sum = 0;

            var sw = Stopwatch.StartNew();
            _entityManager.ForEach<Transform>((int entityIndex, ref Transform t) =>
            {
                iterationCount++;
                sum += t.X + t.Y + t.Z;
            });
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] Query {count} entities (1 component): {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1000 / count:F2}μs per entity");

            Assert.AreEqual(count, iterationCount);
            Assert.Less(sw.ElapsedMilliseconds, 20, "Single component query too slow");
        }

        [Test]
        public void Benchmark_TwoComponentQuery_100k()
        {
            const int count = 100000;

            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Transform { X = i });
                _entityManager.AddComponent(entity, new Velocity { X = 1 });
            }

            int iterationCount = 0;

            var sw = Stopwatch.StartNew();
            _entityManager.ForEach<Transform, Velocity>((int entityIndex, ref Transform t, ref Velocity v) =>
            {
                iterationCount++;
                t.X += v.X;
            });
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] Query {count} entities (2 components): {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1000 / count:F2}μs per entity");

            Assert.AreEqual(count, iterationCount);
            Assert.Less(sw.ElapsedMilliseconds, 50, "Two component query too slow");
        }

        [Test]
        public void Benchmark_ThreeComponentQuery_100k()
        {
            const int count = 100000;

            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Transform { X = i });
                _entityManager.AddComponent(entity, new Velocity { X = 1 });
                _entityManager.AddComponent(entity, new Health { Current = 100, Max = 100 });
            }

            int iterationCount = 0;

            var sw = Stopwatch.StartNew();
            _entityManager.ForEach<Transform, Velocity, Health>(
                (int entityIndex, ref Transform t, ref Velocity v, ref Health h) =>
                {
                    iterationCount++;
                    t.X += v.X;
                    h.Current -= 1;
                });
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] Query {count} entities (3 components): {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1000 / count:F2}μs per entity");

            Assert.AreEqual(count, iterationCount);
            Assert.Less(sw.ElapsedMilliseconds, 100, "Three component query too slow");
        }

        [Test]
        public void Benchmark_ComponentModification_100k()
        {
            const int count = 100000;

            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Transform { X = 0, Y = 0, Z = 0 });
                _entityManager.AddComponent(entity, new Velocity { X = 1, Y = 2, Z = 3 });
            }

            var sw = Stopwatch.StartNew();
            for (int frame = 0; frame < 10; frame++)
            {
                _entityManager.ForEach<Transform, Velocity>((int entityIndex, ref Transform t, ref Velocity v) =>
                {
                    t.X += v.X;
                    t.Y += v.Y;
                    t.Z += v.Z;
                });
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] 10 frames of {count} entity updates: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg per frame: {sw.ElapsedMilliseconds / 10.0:F2}ms");

            Assert.Less(sw.ElapsedMilliseconds, 500, "Component modification too slow");
        }

        [Test]
        public void Benchmark_GroupOperations_100k()
        {
            const int count = 100000;
            var groups = new GroupRegistry();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                groups.AddToGroup<AliveState>(i);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] Add {count} entities to group: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            for (int i = 0; i < count / 2; i++)
            {
                groups.SwapGroup<AliveState, DeadState>(i);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] Swap {count / 2} entities between groups: {sw.ElapsedMilliseconds}ms");

            Assert.AreEqual(count / 2, groups.GetEntitiesInGroup<AliveState>().Count);
            Assert.AreEqual(count / 2, groups.GetEntitiesInGroup<DeadState>().Count);
        }

        [Test]
        public void Benchmark_EntityDestruction_100k()
        {
            const int count = 100000;
            var entities = new Entity[count];

            for (int i = 0; i < count; i++)
            {
                entities[i] = _entityManager.CreateEntity();
                _entityManager.AddComponent(entities[i], new Transform());
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _entityManager.DestroyEntity(entities[i]);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA ECS] Destroy {count} entities: {sw.ElapsedMilliseconds}ms");

            Assert.AreEqual(0, _entityManager.EntityCount);
            Assert.Less(sw.ElapsedMilliseconds, 200, "Entity destruction too slow");
        }

        [Test]
        public void Benchmark_EntityRecycling_100k()
        {
            const int count = 100000;

            for (int cycle = 0; cycle < 3; cycle++)
            {
                var entities = new Entity[count];

                var createSw = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    entities[i] = _entityManager.CreateEntity();
                    _entityManager.AddComponent(entities[i], new Transform());
                }
                createSw.Stop();

                var destroySw = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    _entityManager.DestroyEntity(entities[i]);
                }
                destroySw.Stop();

                UnityEngine.Debug.Log($"[STRADA ECS] Cycle {cycle + 1}: Create {createSw.ElapsedMilliseconds}ms, Destroy {destroySw.ElapsedMilliseconds}ms");
            }

            Assert.AreEqual(0, _entityManager.EntityCount);
        }

        [Test]
        public void Benchmark_MemoryUsage_100k()
        {
            const int count = 100000;

            long memoryBefore = System.GC.GetTotalMemory(true);

            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Transform { X = i, Y = i, Z = i });
                _entityManager.AddComponent(entity, new Velocity { X = 1, Y = 2, Z = 3 });
            }

            long memoryAfter = System.GC.GetTotalMemory(true);
            long usedBytes = memoryAfter - memoryBefore;
            double bytesPerEntity = usedBytes / (double)count;

            UnityEngine.Debug.Log($"[STRADA ECS] Memory for {count} entities (2 components):");
            UnityEngine.Debug.Log($"  Total: {usedBytes / 1024.0:F2} KB");
            UnityEngine.Debug.Log($"  Per entity: {bytesPerEntity:F2} bytes");

            Assert.Less(bytesPerEntity, 128, "Memory usage per entity too high");
        }
    }
}
