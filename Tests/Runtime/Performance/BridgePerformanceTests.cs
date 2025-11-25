using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.DI;
using Strada.Core.ECS;
using UnityEngine;

namespace Strada.Core.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    public class BridgePerformanceTests
    {
        private EntityManager _entityManager;
        private IContainer _container;
        private ViewRegistry _registry;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
            var builder = new ContainerBuilder();
            _container = builder.Build();
            _registry = new ViewRegistry(_entityManager, _container);

            _prefab = new GameObject("BenchmarkPrefab");
            _prefab.AddComponent<BenchmarkView>();
        }

        [TearDown]
        public void TearDown()
        {
            _entityManager?.Dispose();
            _container?.Dispose();
            _registry?.Dispose();

            if (_prefab != null) Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void Benchmark_ViewRegistry_1k_Registrations()
        {
            const int count = 1000;
            var views = new BenchmarkView[count];
            var entities = new Entity[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"View_{i}");
                views[i] = go.AddComponent<BenchmarkView>();
                entities[i] = _entityManager.CreateEntity();
            }

            var swRegister = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _registry.Register(views[i], entities[i]);
            }
            swRegister.Stop();

            var swLookup = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                var view = _registry.GetView(entities[i]);
            }
            swLookup.Stop();

            var swUnregister = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                _registry.Unregister(views[i]);
            }
            swUnregister.Stop();

            double registerAvgNs = swRegister.Elapsed.TotalMilliseconds * 1000000 / count;
            double lookupAvgNs = swLookup.Elapsed.TotalMilliseconds * 1000000 / count;
            double unregisterAvgNs = swUnregister.Elapsed.TotalMilliseconds * 1000000 / count;

            UnityEngine.Debug.Log($"[Bridge BENCHMARK] ViewRegistry ({count} views):");
            UnityEngine.Debug.Log($"  Register: {swRegister.ElapsedMilliseconds}ms total, {registerAvgNs:F0}ns avg");
            UnityEngine.Debug.Log($"  Lookup: {swLookup.ElapsedMilliseconds}ms total, {lookupAvgNs:F0}ns avg");
            UnityEngine.Debug.Log($"  Unregister: {swUnregister.ElapsedMilliseconds}ms total, {unregisterAvgNs:F0}ns avg");

            for (int i = 0; i < count; i++)
            {
                Object.DestroyImmediate(views[i].gameObject);
            }

            Assert.Less(swRegister.ElapsedMilliseconds, 50, "Register too slow");
            Assert.Less(swLookup.ElapsedMilliseconds, 10, "Lookup too slow");
        }

        [Test]
        public void Benchmark_ViewRegistry_SyncAll()
        {
            const int count = 500;
            const int iterations = 100;
            var views = new BenchmarkView[count];
            var entities = new Entity[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"View_{i}");
                views[i] = go.AddComponent<BenchmarkView>();
                entities[i] = _entityManager.CreateEntity();
                _entityManager.AddComponent(entities[i], new TestSyncComponent { Value = i });
                views[i].Bind(_container, _entityManager, entities[i]);
                _registry.Register(views[i], entities[i]);
            }

            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < iterations; iter++)
            {
                _registry.SyncAll();
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
            double avgPerViewNs = sw.Elapsed.TotalMilliseconds * 1000000 / (iterations * count);

            UnityEngine.Debug.Log($"[Bridge BENCHMARK] ViewRegistry.SyncAll ({count} views, {iterations} syncs):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg per sync: {avgMs:F2}ms");
            UnityEngine.Debug.Log($"  Avg per view: {avgPerViewNs:F0}ns");

            for (int i = 0; i < count; i++)
            {
                Object.DestroyImmediate(views[i].gameObject);
            }

            Assert.Less(avgMs, 5, "SyncAll too slow (Target: <5ms for 500 views)");
        }

        [Test]
        public void Benchmark_ViewPool_SpawnDespawn()
        {
            const int cycles = 500;

            var poolRoot = new GameObject("PoolRoot").transform;
            var activeRoot = new GameObject("ActiveRoot").transform;

            var pool = new ViewPool<BenchmarkView>(
                _prefab, _container, _entityManager, _registry,
                poolRoot, activeRoot, initialSize: 100);

            var entities = new Entity[cycles];
            for (int i = 0; i < cycles; i++)
            {
                entities[i] = _entityManager.CreateEntity();
            }

            var swSpawn = Stopwatch.StartNew();
            var views = new BenchmarkView[cycles];
            for (int i = 0; i < cycles; i++)
            {
                views[i] = pool.Spawn(entities[i]);
            }
            swSpawn.Stop();

            var swDespawn = Stopwatch.StartNew();
            for (int i = 0; i < cycles; i++)
            {
                pool.Despawn(views[i]);
            }
            swDespawn.Stop();

            var swRespawn = Stopwatch.StartNew();
            for (int i = 0; i < cycles; i++)
            {
                views[i] = pool.Spawn(entities[i]);
            }
            swRespawn.Stop();

            double spawnAvgUs = swSpawn.Elapsed.TotalMilliseconds * 1000 / cycles;
            double despawnAvgUs = swDespawn.Elapsed.TotalMilliseconds * 1000 / cycles;
            double respawnAvgUs = swRespawn.Elapsed.TotalMilliseconds * 1000 / cycles;

            UnityEngine.Debug.Log($"[Bridge BENCHMARK] ViewPool Spawn/Despawn ({cycles} cycles):");
            UnityEngine.Debug.Log($"  First Spawn: {swSpawn.ElapsedMilliseconds}ms total, {spawnAvgUs:F2}μs avg");
            UnityEngine.Debug.Log($"  Despawn: {swDespawn.ElapsedMilliseconds}ms total, {despawnAvgUs:F2}μs avg");
            UnityEngine.Debug.Log($"  Respawn (pooled): {swRespawn.ElapsedMilliseconds}ms total, {respawnAvgUs:F2}μs avg");
            UnityEngine.Debug.Log($"  Speedup from pooling: {spawnAvgUs / respawnAvgUs:F1}x");

            pool.Dispose();
            Object.DestroyImmediate(poolRoot.gameObject);
            Object.DestroyImmediate(activeRoot.gameObject);

            Assert.Less(respawnAvgUs, spawnAvgUs, "Pooled spawn should be faster than fresh spawn");
        }

        [Test]
        public void Benchmark_EntityBinding_ComponentSync()
        {
            const int iterations = 10000;

            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity, new TestSyncComponent { Value = 0 });

            var binding = new ComponentBinding<TestSyncComponent>(_entityManager, entity);

            var swSet = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                binding.Value = new TestSyncComponent { Value = i };
            }
            swSet.Stop();

            var swSync = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _entityManager.SetComponent(entity, new TestSyncComponent { Value = i });
                binding.Sync();
            }
            swSync.Stop();

            double setAvgNs = swSet.Elapsed.TotalMilliseconds * 1000000 / iterations;
            double syncAvgNs = swSync.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[Bridge BENCHMARK] ComponentBinding ({iterations} operations):");
            UnityEngine.Debug.Log($"  Set: {swSet.ElapsedMilliseconds}ms total, {setAvgNs:F0}ns avg");
            UnityEngine.Debug.Log($"  Sync: {swSync.ElapsedMilliseconds}ms total, {syncAvgNs:F0}ns avg");

            Assert.Less(swSet.ElapsedMilliseconds, 50, "Component binding set too slow");
        }

        [Test]
        public void Benchmark_ComputedProperty()
        {
            const int iterations = 100000;

            var source1 = new ReactiveProperty<int>(0);
            var source2 = new ReactiveProperty<int>(0);
            var computed = ComputedProperty<int>.From(source1, source2, (a, b) => a + b);

            int readCount = 0;
            computed.Subscribe(_ => readCount++);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                source1.Value = i;
            }
            sw.Stop();

            double avgNs = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[Bridge BENCHMARK] ComputedProperty ({iterations} source updates):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNs:F0}ns per update");
            UnityEngine.Debug.Log($"  Notifications fired: {readCount}");

            Assert.Less(sw.ElapsedMilliseconds, 100, "ComputedProperty too slow");
        }

        private struct TestSyncComponent : IComponent
        {
            public int Value;
        }

        private class BenchmarkView : EntityView
        {
            protected override void OnBind() { }
            protected override void OnUnbind() { }
        }
    }
}
