using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Archetypes;
using Strada.Core.ECS.Core;

namespace Strada.Core.Tests.Tests.Runtime.ECS
{
    [TestFixture]
    public class ArchetypeTests
    {
        private EntityManager _entities;
        private ArchetypeManager _archetypes;

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _archetypes = new ArchetypeManager(_entities);
        }

        [TearDown]
        public void TearDown()
        {
            _archetypes?.Dispose();
            _entities?.Dispose();
        }

        [Test]
        public void CreateEntity_WithDescriptor_HasAllComponents()
        {
            var entity = _archetypes.CreateEntity<PlayerDescriptor>();

            Assert.IsTrue(_entities.HasComponent<Position>(entity));
            Assert.IsTrue(_entities.HasComponent<Velocity>(entity));
            Assert.IsTrue(_entities.HasComponent<Health>(entity));
        }

        [Test]
        public void CreateEntity_WithDefaults_HasCorrectValues()
        {
            var entity = _archetypes.CreateEntity<PlayerDescriptor>();

            var health = _entities.GetComponent<Health>(entity);
            Assert.AreEqual(100f, health.Current);
            Assert.AreEqual(100f, health.Max);
        }

        [Test]
        public void CreateEntities_Batch_CreatesCorrectCount()
        {
            var entities = _archetypes.CreateEntities<EnemyDescriptor>(10);

            Assert.AreEqual(10, entities.Length);
            Assert.AreEqual(10, _archetypes.GetEntityCount<EnemyDescriptor>());
        }

        [Test]
        public void GetEntities_ReturnsTrackedEntities()
        {
            _archetypes.CreateEntity<PlayerDescriptor>();
            _archetypes.CreateEntity<PlayerDescriptor>();
            _archetypes.CreateEntity<EnemyDescriptor>();

            var players = _archetypes.GetEntities<PlayerDescriptor>();
            var enemies = _archetypes.GetEntities<EnemyDescriptor>();

            Assert.AreEqual(2, players.Count);
            Assert.AreEqual(1, enemies.Count);
        }

        [Test]
        public void DestroyEntity_RemovesFromTracking()
        {
            var entity = _archetypes.CreateEntity<PlayerDescriptor>();
            Assert.AreEqual(1, _archetypes.GetEntityCount<PlayerDescriptor>());

            _archetypes.DestroyEntity<PlayerDescriptor>(entity);
            Assert.AreEqual(0, _archetypes.GetEntityCount<PlayerDescriptor>());
        }

        private struct Position : IComponent { public float X, Y, Z; }
        private struct Velocity : IComponent { public float X, Y, Z; }
        private struct Health : IComponent { public float Current, Max; }
        private struct AITarget : IComponent { public int TargetId; }

        private class PlayerDescriptor : EntityDescriptor
        {
            protected override void Define()
            {
                Add<Position>();
                Add<Velocity>();
                Add(new Health { Current = 100, Max = 100 });
            }
        }

        private class EnemyDescriptor : EntityDescriptor
        {
            protected override void Define()
            {
                Add<Position>();
                Add<Velocity>();
                Add(new Health { Current = 50, Max = 50 });
                Add<AITarget>();
            }
        }
    }

    [TestFixture]
    [Category("Performance")]
    public class ArchetypePerformanceTests
    {
        [Test]
        public void Benchmark_10k_EntityCreation_WithDescriptor()
        {
            var entities = new EntityManager();
            var archetypes = new ArchetypeManager(entities);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10_000; i++)
                archetypes.CreateEntity<BenchDescriptor>();
            sw.Stop();

            UnityEngine.Debug.Log($"[Archetype] 10k entity creation: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 10_000 / System.Diagnostics.Stopwatch.Frequency * 1_000_000:F0}ns/entity)");

            Assert.AreEqual(10_000, archetypes.GetEntityCount<BenchDescriptor>());
            Assert.Less(sw.ElapsedMilliseconds, 100);

            archetypes.Dispose();
            entities.Dispose();
        }

        [Test]
        public void Benchmark_10k_BatchCreation()
        {
            var entities = new EntityManager();
            var archetypes = new ArchetypeManager(entities);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            archetypes.CreateEntities<BenchDescriptor>(10_000);
            sw.Stop();

            UnityEngine.Debug.Log($"[Archetype] 10k batch creation: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 10_000 / System.Diagnostics.Stopwatch.Frequency * 1_000_000:F0}ns/entity)");

            Assert.AreEqual(10_000, archetypes.GetEntityCount<BenchDescriptor>());
            Assert.Less(sw.ElapsedMilliseconds, 100);

            archetypes.Dispose();
            entities.Dispose();
        }

        private struct BenchPosition : IComponent { public float X, Y, Z; }
        private struct BenchVelocity : IComponent { public float Vx, Vy, Vz; }
        private struct BenchHealth : IComponent { public float Current, Max; }

        private class BenchDescriptor : EntityDescriptor
        {
            protected override void Define()
            {
                Add<BenchPosition>();
                Add<BenchVelocity>();
                Add(new BenchHealth { Current = 100, Max = 100 });
            }
        }
    }
}
