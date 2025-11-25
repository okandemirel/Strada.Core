using System;
using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.MVCS;
using UnityEngine;

namespace Strada.Core.Tests.Bridge
{
    [TestFixture]
    public class BridgeTests
    {
        private EntityManager _entities;
        private ContainerBuilder _builder;
        private IContainer _container;

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _builder = new ContainerBuilder();
            _builder.RegisterInstance(_entities);
            _container = _builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
            _entities?.Dispose();
        }

        [Test]
        public void ComponentBinding_Sync_DetectsChanges()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100 });

            var detectedValue = 0f;
            var binding = new ComponentBinding<HealthComponent, float>(
                _entities,
                entity,
                c => c.Current,
                v => detectedValue = v);

            _entities.SetComponent(entity, new HealthComponent { Current = 50 });
            binding.Sync();

            Assert.AreEqual(50f, detectedValue);
        }

        [Test]
        public void ComponentBinding_Push_UpdatesComponent()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            var binding = new ComponentBinding<HealthComponent, float>(
                _entities,
                entity,
                c => c.Current,
                (c, v) => new HealthComponent { Current = v, Max = c.Max },
                _ => { });

            binding.Push(75f);

            var component = _entities.GetComponent<HealthComponent>(entity);
            Assert.AreEqual(75f, component.Current);
        }

        [Test]
        public void AutoSyncBinding_DetectsChanges()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new PositionComponent { X = 0, Y = 0 });

            PositionComponent lastDetected = default;
            var binding = new AutoSyncBinding<PositionComponent>(
                _entities,
                entity,
                c => lastDetected = c);

            _entities.SetComponent(entity, new PositionComponent { X = 10, Y = 20 });
            binding.Sync();

            Assert.AreEqual(10f, lastDetected.X);
            Assert.AreEqual(20f, lastDetected.Y);
        }

        [Test]
        public void MediatorRegistry_CreateAndRelease()
        {
            var registry = new MediatorRegistry(_container);
            var entity = _entities.CreateEntity();
            var go = new GameObject("TestView");
            var view = go.AddComponent<TestView>();

            var mediator = registry.Create<TestMediator, TestView>(entity, view);

            Assert.IsNotNull(mediator);
            Assert.IsTrue(mediator.IsBound);
            Assert.AreEqual(1, registry.ActiveCount);

            registry.Release<TestMediator, TestView>(mediator);

            Assert.IsFalse(mediator.IsBound);
            Assert.AreEqual(0, registry.ActiveCount);

            UnityEngine.Object.DestroyImmediate(go);
            registry.Dispose();
        }

        [Test]
        public void MediatorRegistry_PoolsMediator()
        {
            var registry = new MediatorRegistry(_container);
            var entity = _entities.CreateEntity();
            var go = new GameObject("TestView");
            var view = go.AddComponent<TestView>();

            var mediator1 = registry.Create<TestMediator, TestView>(entity, view);
            registry.Release<TestMediator, TestView>(mediator1);

            var mediator2 = registry.Create<TestMediator, TestView>(entity, view);

            Assert.AreSame(mediator1, mediator2);

            registry.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
        }

        private struct HealthComponent : IComponent
        {
            public float Current;
            public float Max;
        }

        private struct PositionComponent : IComponent
        {
            public float X;
            public float Y;
        }

        private class TestView : StradaView { }

        private class TestMediator : ViewMediator<TestView>
        {
            protected override void OnBind() { }
            protected override void OnUnbind() { }
        }
    }

    [TestFixture]
    [Category("Performance")]
    public class BridgePerformanceTests
    {
        [Test]
        public void Benchmark_100k_BindingSync()
        {
            var entities = new EntityManager();
            var entity = entities.CreateEntity();
            entities.AddComponent(entity, new BenchComponent { Value = -1 });

            var count = 0;
            var binding = new AutoSyncBinding<BenchComponent>(entities, entity, c => count++);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
            {
                entities.SetComponent(entity, new BenchComponent { Value = i });
                binding.Sync();
            }
            sw.Stop();

            Debug.Log($"[Bridge] 100k binding syncs: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 100_000 / System.Diagnostics.Stopwatch.Frequency * 1_000_000:F0}ns/sync)");

            Assert.AreEqual(100_000, count);
            Assert.Less(sw.ElapsedMilliseconds, 200);

            entities.Dispose();
        }

        private struct BenchComponent : IComponent
        {
            public int Value;
        }
    }
}
