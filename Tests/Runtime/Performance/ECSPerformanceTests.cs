using NUnit.Framework;
using Strada.Core.ECS;
using System.Diagnostics;
using Unity.Mathematics;

namespace Strada.Core.Tests.Performance
{
    public struct PositionComponent : IStradaComponent
    {
        public float3 Value;
    }

    public struct VelocityComponent : IStradaComponent
    {
        public float3 Value;
    }

    public struct RotationComponent : IStradaComponent
    {
        public float4 Value;
    }

    [TestFixture]
    public class ECSPerformanceTests
    {
        private IStradaWorld _world;
        private EntityManager _entityManager;

        [SetUp]
        public void Setup()
        {
            _world = StradaWorld.Create("PerformanceTest");
            _entityManager = (EntityManager)_world.EntityManager;
        }

        [Test]
        public void Benchmark_Entity_Creation()
        {
            const int entityCount = 100000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                var entity = _entityManager.CreateEntity();
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA BENCHMARK] {entityCount} Entity Creations:");
            UnityEngine.Debug.Log($"  Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1_000_000 / entityCount:F2}ns per entity");

            Assert.Less(sw.ElapsedMilliseconds, 100, "Entity creation too slow");
        }

        [Test]
        public void Benchmark_Component_Operations()
        {
            const int entityCount = 10000;
            var entities = new Entity[entityCount];

            for (int i = 0; i < entityCount; i++)
            {
                entities[i] = _entityManager.CreateEntity();
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                _entityManager.AddComponent(entities[i], new PositionComponent { Value = float3.zero });
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA BENCHMARK] {entityCount} Component Additions:");
            UnityEngine.Debug.Log($"  Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1_000_000 / entityCount:F2}ns per add");

            Assert.Less(sw.ElapsedMilliseconds, 50, "Component add too slow");
        }

        [Test]
        public void Benchmark_Query_Creation()
        {
            const int entityCount = 10000;

            for (int i = 0; i < entityCount; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new PositionComponent { Value = new float3(i, i, i) });
                _entityManager.AddComponent(entity, new VelocityComponent { Value = new float3(1, 1, 1) });
            }

            var sw = Stopwatch.StartNew();
            var query = _entityManager.Query<PositionComponent, VelocityComponent>();
            var entities = query.GetEntities();
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA BENCHMARK] Query Creation ({entityCount} entities):");
            UnityEngine.Debug.Log($"  Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Entities Found: {entities.Count}");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1_000_000 / entityCount:F2}ns per entity");

            Assert.AreEqual(entityCount, entities.Count, "Query did not find all entities");
            Assert.Less(sw.ElapsedMilliseconds, 50, "Query creation too slow");
        }

        [Test]
        public void Benchmark_Batch_Operations()
        {
            const int entityCount = 10000;
            var entities = new Entity[entityCount];

            for (int i = 0; i < entityCount; i++)
            {
                entities[i] = _entityManager.CreateEntity();
            }

            var component = new PositionComponent { Value = float3.zero };

            var sw = Stopwatch.StartNew();
            _entityManager.AddComponentBatch(entities, component);
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA BENCHMARK] Batch Add {entityCount} Components:");
            UnityEngine.Debug.Log($"  Time: {sw.ElapsedMilliseconds}ms");

            Assert.Less(sw.ElapsedMilliseconds, 20, "Batch operations too slow");
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }
    }
}
