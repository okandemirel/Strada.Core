using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Query;

namespace Strada.Core.Tests.Performance
{
    public struct Position : IComponent
    {
        public float X, Y, Z;
    }

    public struct Rotation : IComponent
    {
        public float X, Y, Z, W;
    }

    public struct Scale : IComponent
    {
        public float X, Y, Z;
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

    public struct Damage : IComponent
    {
        public int Amount;
    }

    public struct Tag : IComponent
    {
        public int Id;
    }

    [TestFixture]
    [Category("Performance")]
    public class ECSPerformanceTests
    {
        private EntityManager _entityManager;
        private const int WarmupIterations = 100;

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
        public void Benchmark_EntityCreation_Simple_100k()
        {
            const int count = 100_000;

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                var e = _entityManager.CreateEntity();
                _entityManager.DestroyEntity(e);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _entityManager.CreateEntity();
            }
            sw.Stop();

            double usPerOp = (sw.Elapsed.TotalMilliseconds * 1000) / count;
            double nsPerOp = usPerOp * 1000;

            UnityEngine.Debug.Log($"=== STRADA ECS: Entity Creation ({count:N0} entities) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-entity: {nsPerOp:F1}ns ({usPerOp:F3}μs)");

            Assert.AreEqual(count, _entityManager.EntityCount);
            Assert.Less(usPerOp, 1.0, "Entity creation should be under 1μs");
        }

        [Test]
        public void Benchmark_EntityCreation_WithComponent_100k()
        {
            const int count = 100_000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = i, Y = i, Z = i });
            }
            sw.Stop();

            double usPerOp = (sw.Elapsed.TotalMilliseconds * 1000) / count;

            UnityEngine.Debug.Log($"=== STRADA ECS: Entity + 1 Component ({count:N0} entities) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-entity: {usPerOp:F3}μs");

            Assert.Less(usPerOp, 2.0, "Entity + component should be under 2μs");
        }

        [Test]
        public void Benchmark_EntityCreation_With3Components_100k()
        {
            const int count = 100_000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = i, Y = i, Z = i });
                _entityManager.AddComponent(entity, new Velocity { X = 1, Y = 0, Z = 0 });
                _entityManager.AddComponent(entity, new Health { Current = 100, Max = 100 });
            }
            sw.Stop();

            double usPerOp = (sw.Elapsed.TotalMilliseconds * 1000) / count;

            UnityEngine.Debug.Log($"=== STRADA ECS: Entity + 3 Components ({count:N0} entities) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-entity: {usPerOp:F3}μs");

            Assert.Less(usPerOp, 5.0, "Entity + 3 components should be under 5μs");
        }

        [Test]
        public void Benchmark_Query_SingleComponent_100k()
        {
            const int count = 100_000;

            // Setup entities
            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = i, Y = i, Z = i });
            }

            // Warmup query
            int warmupCount = 0;
            _entityManager.ForEach<Position>((int idx, ref Position p) => warmupCount++);

            int iterCount = 0;
            float sum = 0;

            var sw = Stopwatch.StartNew();
            _entityManager.ForEach<Position>((int idx, ref Position p) =>
            {
                iterCount++;
                sum += p.X + p.Y + p.Z;
            });
            sw.Stop();

            double usPerEntity = (sw.Elapsed.TotalMilliseconds * 1000) / count;
            double nsPerEntity = usPerEntity * 1000;

            UnityEngine.Debug.Log($"=== STRADA ECS: Single Component Query ({count:N0} entities) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-entity: {nsPerEntity:F1}ns ({usPerEntity:F4}μs)");

            Assert.AreEqual(count, iterCount);
            Assert.Less(usPerEntity, 0.1, "Single component query should be under 0.1μs per entity");
        }

        [Test]
        public void Benchmark_Query_TwoComponents_100k()
        {
            const int count = 100_000;

            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = i });
                _entityManager.AddComponent(entity, new Velocity { X = 1 });
            }

            int iterCount = 0;

            var sw = Stopwatch.StartNew();
            _entityManager.ForEach<Position, Velocity>((int idx, ref Position p, ref Velocity v) =>
            {
                iterCount++;
                p.X += v.X;
                p.Y += v.Y;
                p.Z += v.Z;
            });
            sw.Stop();

            double usPerEntity = (sw.Elapsed.TotalMilliseconds * 1000) / count;
            double nsPerEntity = usPerEntity * 1000;

            UnityEngine.Debug.Log($"=== STRADA ECS: Two Component Query ({count:N0} entities) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-entity: {nsPerEntity:F1}ns ({usPerEntity:F4}μs)");

            Assert.AreEqual(count, iterCount);
            Assert.Less(usPerEntity, 0.2, "Two component query should be under 0.2μs per entity");
        }

        [Test]
        public void Benchmark_Query_ThreeComponents_100k()
        {
            const int count = 100_000;

            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = i });
                _entityManager.AddComponent(entity, new Velocity { X = 1 });
                _entityManager.AddComponent(entity, new Health { Current = 100, Max = 100 });
            }

            int iterCount = 0;

            var sw = Stopwatch.StartNew();
            _entityManager.ForEach<Position, Velocity, Health>((int idx, ref Position p, ref Velocity v, ref Health h) =>
            {
                iterCount++;
                p.X += v.X;
                h.Current -= 1;
            });
            sw.Stop();

            double usPerEntity = (sw.Elapsed.TotalMilliseconds * 1000) / count;
            double nsPerEntity = usPerEntity * 1000;

            UnityEngine.Debug.Log($"=== STRADA ECS: Three Component Query ({count:N0} entities) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-entity: {nsPerEntity:F1}ns ({usPerEntity:F4}μs)");

            Assert.AreEqual(count, iterCount);
            Assert.Less(usPerEntity, 0.3, "Three component query should be under 0.3μs per entity");
        }

        [Test]
        public void Benchmark_SimulationLoop_10Frames_100k()
        {
            const int entityCount = 100_000;
            const int frameCount = 10;

            // Create simulation entities
            for (int i = 0; i < entityCount; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = 0, Y = 0, Z = 0 });
                _entityManager.AddComponent(entity, new Velocity { X = 1, Y = 0.5f, Z = 0.1f });
            }

            // Warmup
            _entityManager.ForEach<Position, Velocity>((int idx, ref Position p, ref Velocity v) =>
            {
                p.X += v.X;
                p.Y += v.Y;
                p.Z += v.Z;
            });

            var sw = Stopwatch.StartNew();
            for (int frame = 0; frame < frameCount; frame++)
            {
                _entityManager.ForEach<Position, Velocity>((int idx, ref Position p, ref Velocity v) =>
                {
                    p.X += v.X;
                    p.Y += v.Y;
                    p.Z += v.Z;
                });
            }
            sw.Stop();

            double msPerFrame = sw.Elapsed.TotalMilliseconds / frameCount;
            double usPerEntity = (sw.Elapsed.TotalMilliseconds * 1000) / (frameCount * entityCount);

            UnityEngine.Debug.Log($"=== STRADA ECS: Simulation Loop ({entityCount:N0} entities, {frameCount} frames) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-frame: {msPerFrame:F2}ms");
            UnityEngine.Debug.Log($"  Per-entity-frame: {usPerEntity * 1000:F1}ns");

            Assert.Less(msPerFrame, 10, "Per frame should be under 10ms for 100k entities");
        }

        [Test]
        public void Benchmark_EntityDestruction_100k()
        {
            const int count = 100_000;
            var entities = new Entity[count];

            for (int i = 0; i < count; i++)
            {
                entities[i] = _entityManager.CreateEntity();
                _entityManager.AddComponent(entities[i], new Position());
                _entityManager.AddComponent(entities[i], new Velocity());
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _entityManager.DestroyEntity(entities[i]);
            }
            sw.Stop();

            double usPerOp = (sw.Elapsed.TotalMilliseconds * 1000) / count;

            UnityEngine.Debug.Log($"=== STRADA ECS: Entity Destruction ({count:N0} entities) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-entity: {usPerOp:F3}μs");

            Assert.AreEqual(0, _entityManager.EntityCount);
            Assert.Less(usPerOp, 2.0, "Entity destruction should be under 2μs");
        }

        [Test]
        public void Benchmark_EntityRecycling_3Cycles_100k()
        {
            const int count = 100_000;

            UnityEngine.Debug.Log($"=== STRADA ECS: Entity Recycling ({count:N0} entities, 3 cycles) ===");

            for (int cycle = 0; cycle < 3; cycle++)
            {
                var entities = new Entity[count];

                var createSw = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    entities[i] = _entityManager.CreateEntity();
                    _entityManager.AddComponent(entities[i], new Position());
                }
                createSw.Stop();

                var destroySw = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    _entityManager.DestroyEntity(entities[i]);
                }
                destroySw.Stop();

                UnityEngine.Debug.Log($"  Cycle {cycle + 1}: Create {createSw.ElapsedMilliseconds}ms, Destroy {destroySw.ElapsedMilliseconds}ms");
            }

            Assert.AreEqual(0, _entityManager.EntityCount);
        }

        [Test]
        public void Benchmark_ComponentAddRemove_100k()
        {
            const int count = 100_000;

            // Create entities without component
            var entities = new Entity[count];
            for (int i = 0; i < count; i++)
            {
                entities[i] = _entityManager.CreateEntity();
            }

            // Benchmark adding components
            var addSw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _entityManager.AddComponent(entities[i], new Position { X = i });
            }
            addSw.Stop();

            // Benchmark removing components
            var removeSw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _entityManager.RemoveComponent<Position>(entities[i]);
            }
            removeSw.Stop();

            double addUs = (addSw.Elapsed.TotalMilliseconds * 1000) / count;
            double removeUs = (removeSw.Elapsed.TotalMilliseconds * 1000) / count;

            UnityEngine.Debug.Log($"=== STRADA ECS: Component Add/Remove ({count:N0} operations) ===");
            UnityEngine.Debug.Log($"  Add: {addSw.ElapsedMilliseconds}ms ({addUs:F3}μs/op)");
            UnityEngine.Debug.Log($"  Remove: {removeSw.ElapsedMilliseconds}ms ({removeUs:F3}μs/op)");

            Assert.Less(addUs, 1.0, "Component add should be under 1μs");
            Assert.Less(removeUs, 1.0, "Component remove should be under 1μs");
        }

        [Test]
        public void Benchmark_HasComponent_100k()
        {
            const int count = 100_000;
            var entities = new Entity[count];

            for (int i = 0; i < count; i++)
            {
                entities[i] = _entityManager.CreateEntity();
                if (i % 2 == 0)
                    _entityManager.AddComponent(entities[i], new Position());
            }

            int hasCount = 0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                if (_entityManager.HasComponent<Position>(entities[i]))
                    hasCount++;
            }
            sw.Stop();

            double nsPerOp = sw.Elapsed.TotalMilliseconds * 1000 * 1000 / count;

            UnityEngine.Debug.Log($"=== STRADA ECS: HasComponent Check ({count:N0} checks) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-check: {nsPerOp:F1}ns");

            Assert.AreEqual(count / 2, hasCount);
            Assert.Less(nsPerOp, 100, "HasComponent should be under 100ns");
        }

        [Test]
        public void Benchmark_GetComponent_100k()
        {
            const int count = 100_000;
            var entities = new Entity[count];

            for (int i = 0; i < count; i++)
            {
                entities[i] = _entityManager.CreateEntity();
                _entityManager.AddComponent(entities[i], new Position { X = i });
            }

            float sum = 0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                var pos = _entityManager.GetComponent<Position>(entities[i]);
                sum += pos.X;
            }
            sw.Stop();

            double nsPerOp = sw.Elapsed.TotalMilliseconds * 1000 * 1000 / count;

            UnityEngine.Debug.Log($"=== STRADA ECS: GetComponent ({count:N0} gets) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-get: {nsPerOp:F1}ns");

            Assert.Less(nsPerOp, 100, "GetComponent should be under 100ns");
        }

        [Test]
        public void Benchmark_SetComponent_100k()
        {
            const int count = 100_000;
            var entities = new Entity[count];

            for (int i = 0; i < count; i++)
            {
                entities[i] = _entityManager.CreateEntity();
                _entityManager.AddComponent(entities[i], new Position { X = 0 });
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _entityManager.SetComponent(entities[i], new Position { X = i, Y = i, Z = i });
            }
            sw.Stop();

            double nsPerOp = sw.Elapsed.TotalMilliseconds * 1000 * 1000 / count;

            UnityEngine.Debug.Log($"=== STRADA ECS: SetComponent ({count:N0} sets) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-set: {nsPerOp:F1}ns");

            Assert.Less(nsPerOp, 100, "SetComponent should be under 100ns");
        }

        [Test]
        public void Benchmark_MemoryUsage_100k()
        {
            const int count = 100_000;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = i, Y = i, Z = i });
                _entityManager.AddComponent(entity, new Velocity { X = 1, Y = 2, Z = 3 });
            }

            long memAfter = GC.GetTotalMemory(true);
            long usedBytes = memAfter - memBefore;
            double bytesPerEntity = usedBytes / (double)count;

            // Calculate theoretical minimum
            // Position: 12 bytes (3 floats), Velocity: 12 bytes, Entity index: 4 bytes = 28 bytes minimum
            double theoreticalMin = 28;

            UnityEngine.Debug.Log($"=== STRADA ECS: Memory Usage ({count:N0} entities, 2 components each) ===");
            UnityEngine.Debug.Log($"  Total: {usedBytes / 1024.0:F2} KB ({usedBytes / 1024.0 / 1024.0:F2} MB)");
            UnityEngine.Debug.Log($"  Per-entity: {bytesPerEntity:F1} bytes");
            UnityEngine.Debug.Log($"  Theoretical min: {theoreticalMin} bytes");
            UnityEngine.Debug.Log($"  Overhead: {(bytesPerEntity / theoreticalMin - 1) * 100:F1}%");

            Assert.Less(bytesPerEntity, 128, "Memory per entity should be under 128 bytes");
        }

        [Test]
        public void Benchmark_MixedEntityTypes_100k()
        {
            const int count = 100_000;

            // Create entities with different component combinations
            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = i });

                if (i % 2 == 0)
                    _entityManager.AddComponent(entity, new Velocity { X = 1 });
                if (i % 3 == 0)
                    _entityManager.AddComponent(entity, new Health { Current = 100 });
                if (i % 5 == 0)
                    _entityManager.AddComponent(entity, new Tag { Id = i });
            }

            int posVelCount = 0;
            int posHealthCount = 0;

            var sw = Stopwatch.StartNew();

            // Query Position + Velocity (50% of entities)
            _entityManager.ForEach<Position, Velocity>((int idx, ref Position p, ref Velocity v) =>
            {
                posVelCount++;
                p.X += v.X;
            });

            // Query Position + Health (33% of entities)
            _entityManager.ForEach<Position, Health>((int idx, ref Position p, ref Health h) =>
            {
                posHealthCount++;
                h.Current -= 1;
            });

            sw.Stop();

            UnityEngine.Debug.Log($"=== STRADA ECS: Mixed Entity Types ({count:N0} entities) ===");
            UnityEngine.Debug.Log($"  Position+Velocity matches: {posVelCount:N0}");
            UnityEngine.Debug.Log($"  Position+Health matches: {posHealthCount:N0}");
            UnityEngine.Debug.Log($"  Total query time: {sw.Elapsed.TotalMilliseconds:F2}ms");

            Assert.AreEqual(count / 2, posVelCount);
            Assert.AreEqual(count / 3 + 1, posHealthCount); // +1 for entity 0
        }

        [Test]
        public void Benchmark_Comparison_ManualVsECS()
        {
            const int count = 100_000;
            const int frames = 10;

            // Manual arrays approach
            var manualPositions = new Position[count];
            var manualVelocities = new Velocity[count];
            for (int i = 0; i < count; i++)
            {
                manualPositions[i] = new Position { X = 0, Y = 0, Z = 0 };
                manualVelocities[i] = new Velocity { X = 1, Y = 0.5f, Z = 0.1f };
            }

            // ECS approach
            for (int i = 0; i < count; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new Position { X = 0, Y = 0, Z = 0 });
                _entityManager.AddComponent(entity, new Velocity { X = 1, Y = 0.5f, Z = 0.1f });
            }

            // Warmup both
            for (int i = 0; i < count; i++)
            {
                manualPositions[i].X += manualVelocities[i].X;
            }
            _entityManager.ForEach<Position, Velocity>((int idx, ref Position p, ref Velocity v) => p.X += v.X);

            // Benchmark manual
            var swManual = Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < count; i++)
                {
                    manualPositions[i].X += manualVelocities[i].X;
                    manualPositions[i].Y += manualVelocities[i].Y;
                    manualPositions[i].Z += manualVelocities[i].Z;
                }
            }
            swManual.Stop();

            // Benchmark ECS
            var swECS = Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
            {
                _entityManager.ForEach<Position, Velocity>((int idx, ref Position p, ref Velocity v) =>
                {
                    p.X += v.X;
                    p.Y += v.Y;
                    p.Z += v.Z;
                });
            }
            swECS.Stop();

            double manualMs = swManual.Elapsed.TotalMilliseconds;
            double ecsMs = swECS.Elapsed.TotalMilliseconds;
            double overhead = ecsMs / manualMs;

            UnityEngine.Debug.Log($"=== STRADA ECS vs Manual Arrays ({count:N0} entities, {frames} frames) ===");
            UnityEngine.Debug.Log($"  Manual arrays: {manualMs:F2}ms");
            UnityEngine.Debug.Log($"  ECS ForEach:   {ecsMs:F2}ms");
            UnityEngine.Debug.Log($"  ECS Overhead:  {overhead:F2}x");

            // ECS should be within 10x of manual array iteration (realistic for managed ECS)
            Assert.Less(overhead, 10.0, "ECS overhead should be less than 10x manual arrays");
        }
    }
}
