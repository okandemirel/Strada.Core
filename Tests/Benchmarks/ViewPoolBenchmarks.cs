using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.Sync;
using UnityEngine;

namespace Strada.Core.Tests.Benchmarks
{
    [TestFixture]
    [Category("Benchmark")]
    public class ViewPoolBenchmarks
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
            builder.RegisterFactory<IContainer>(_ => _container, Lifetime.Singleton);
            _container = builder.Build();
            _registry = new ViewRegistry(_entityManager, _container);

            // Create a prefab with EntityView
            _prefab = new GameObject("TestPrefab");
            _prefab.AddComponent<TestEntityView>();
        }

        [TearDown]
        public void TearDown()
        {
            _registry?.Dispose();
            _entityManager?.Dispose();
            _container?.Dispose();
            if (_prefab != null)
                UnityEngine.Object.DestroyImmediate(_prefab);
        }

        [Test]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(500)]
        public void ViewPool_Despawn_ByEntity_Scaling(int viewCount)
        {
            var pool = new ViewPool<TestEntityView>(
                _prefab, _container, _entityManager, _registry,
                initialSize: 0, maxSize: viewCount + 100);

            // Spawn all views
            var entities = new Entity[viewCount];
            for (int i = 0; i < viewCount; i++)
            {
                entities[i] = _entityManager.CreateEntity();
                pool.Spawn(entities[i]);
            }

            // Warmup
            for (int i = 0; i < Math.Min(10, viewCount); i++)
            {
                pool.Despawn(entities[i]);
                pool.Spawn(entities[i]);
            }

            // Benchmark despawn by entity
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < viewCount; i++)
            {
                pool.Despawn(entities[i]);
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / viewCount;

            UnityEngine.Debug.Log($"[ViewPool] Despawn by Entity ({viewCount} views):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}us per despawn");

            pool.Dispose();
        }

        [Test]
        public void ViewRegistry_Register_Unregister_Scaling()
        {
            const int viewCount = 500;

            var entities = new Entity[viewCount];
            var views = new TestEntityView[viewCount];

            for (int i = 0; i < viewCount; i++)
            {
                entities[i] = _entityManager.CreateEntity();
                var go = new GameObject($"View_{i}");
                views[i] = go.AddComponent<TestEntityView>();
            }

            // Benchmark register
            var swRegister = Stopwatch.StartNew();
            for (int i = 0; i < viewCount; i++)
            {
                _registry.Register(views[i], entities[i]);
            }
            swRegister.Stop();

            // Benchmark unregister
            var swUnregister = Stopwatch.StartNew();
            for (int i = 0; i < viewCount; i++)
            {
                _registry.Unregister(views[i]);
            }
            swUnregister.Stop();

            double avgRegisterUs = swRegister.Elapsed.TotalMilliseconds * 1000 / viewCount;
            double avgUnregisterUs = swUnregister.Elapsed.TotalMilliseconds * 1000 / viewCount;

            UnityEngine.Debug.Log($"[ViewRegistry] Scaling ({viewCount} views):");
            UnityEngine.Debug.Log($"  Register - Total: {swRegister.ElapsedMilliseconds}ms, Avg: {avgRegisterUs:F2}us");
            UnityEngine.Debug.Log($"  Unregister - Total: {swUnregister.ElapsedMilliseconds}ms, Avg: {avgUnregisterUs:F2}us");

            // Cleanup
            foreach (var view in views)
            {
                if (view != null && view.gameObject != null)
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        private class TestEntityView : EntityView
        {
            protected override void OnBind() { }
        }
    }
}
