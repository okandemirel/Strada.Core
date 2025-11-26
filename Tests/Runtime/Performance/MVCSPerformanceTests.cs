using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.Modules;
using Strada.Core.MVCS;

namespace Strada.Core.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    public class MVCSPerformanceTests
    {
        private IContainer _container;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.Register<StradaBus>(Lifetime.Singleton);
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
        }

        [Test]
        public void Benchmark_InjectionProcessor_10k_Injections()
        {
            const int iterations = 10000;
            const int warmup = 100;

            for (int i = 0; i < warmup; i++)
            {
                var service = new BenchmarkService();
                InjectionProcessor.Inject(service, _container);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var service = new BenchmarkService();
                InjectionProcessor.Inject(service, _container);
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] InjectionProcessor ({iterations} injections):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per injection");

            Assert.Less(sw.ElapsedMilliseconds, 100, "Injection too slow (Target: <100ms for 10k)");
        }

        [Test]
        public void Benchmark_ReactiveProperty_100k_Updates()
        {
            const int iterations = 100000;
            const int warmup = 1000;

            var property = new ReactiveProperty<int>(0);
            int notifyCount = 0;
            property.Subscribe(_ => notifyCount++);

            for (int i = 0; i < warmup; i++)
                property.Value = i;

            notifyCount = 0;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                property.Value = i;
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] ReactiveProperty Updates ({iterations} updates):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Notifications: {notifyCount}");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per update");

            Assert.Less(sw.ElapsedMilliseconds, 50, "ReactiveProperty updates too slow");
        }

        [Test]
        public void Benchmark_ReactiveProperty_MultipleSubscribers()
        {
            const int subscribers = 10;
            const int iterations = 10000;

            var property = new ReactiveProperty<int>(0);
            int[] counts = new int[subscribers];

            for (int s = 0; s < subscribers; s++)
            {
                int idx = s;
                property.Subscribe(_ => counts[idx]++);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                property.Value = i;
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] ReactiveProperty with {subscribers} subscribers ({iterations} updates):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per update (dispatching to {subscribers} subscribers)");

            Assert.Less(sw.ElapsedMilliseconds, 100, "ReactiveProperty multi-subscriber too slow");
        }

        [Test]
        public void Benchmark_ContainerScope_10k_Resolutions()
        {
            const int iterations = 10000;
            const int warmup = 100;

            var builder = new ContainerBuilder();
            builder.Register<LocalService>(Lifetime.Singleton);
            var scopedContainer = builder.Build();

            for (int i = 0; i < warmup; i++)
                scopedContainer.Resolve<LocalService>();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var svc = scopedContainer.Resolve<LocalService>();
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Container Resolution ({iterations} resolutions):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per resolution");

            scopedContainer.Dispose();

            Assert.Less(sw.ElapsedMilliseconds, 10, "Container resolution too slow");
        }

        [Test]
        public void Benchmark_ContainerScope_NestedScopes()
        {
            const int depth = 10;
            const int iterations = 10000;

            var builder = new ContainerBuilder();
            builder.Register<LocalService>(Lifetime.Scoped);
            var rootContainer = builder.Build();

            IContainerScope current = rootContainer.CreateScope();
            for (int d = 0; d < depth - 1; d++)
            {
                current = current.CreateScope();
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var svc = current.Resolve<LocalService>();
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Nested Scope ({depth} levels, {iterations} resolutions):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per resolution");

            ((IDisposable)current).Dispose();
            rootContainer.Dispose();

            Assert.Less(sw.ElapsedMilliseconds, 20, "Nested scope resolution too slow");
        }

        [Test]
        public void Benchmark_Controller_Lifecycle()
        {
            const int iterations = 1000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var controller = new BenchmarkController();
                InjectionProcessor.Inject(controller, _container);
                controller.Initialize();
                controller.Dispose();
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Controller Full Lifecycle ({iterations} cycles):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per cycle (create+inject+init+dispose)");

            Assert.Less(sw.ElapsedMilliseconds, 500, "Controller lifecycle too slow");
        }

        [Test]
        public void Benchmark_Model_PropertyCreation()
        {
            const int properties = 100;
            const int iterations = 1000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var model = new MultiPropertyModel(properties);
                model.Initialize();
                model.Dispose();
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Model with {properties} Properties ({iterations} cycles):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per cycle");

            Assert.Less(sw.ElapsedMilliseconds, 1000, "Model property creation too slow");
        }

        [Test]
        public void Benchmark_ReactiveCollection_Operations()
        {
            const int items = 10000;
            const int warmup = 100;

            var collection = new ReactiveCollection<int>();
            int addCount = 0;
            int removeCount = 0;

            collection.OnAdd(_ => addCount++);
            collection.OnRemove(_ => removeCount++);

            for (int i = 0; i < warmup; i++)
            {
                collection.Add(i);
                collection.Remove(i);
            }
            collection.Clear();
            addCount = 0;
            removeCount = 0;

            var swAdd = Stopwatch.StartNew();
            for (int i = 0; i < items; i++)
            {
                collection.Add(i);
            }
            swAdd.Stop();

            var swRemove = Stopwatch.StartNew();
            for (int i = items - 1; i >= 0; i--)
            {
                collection.Remove(i);
            }
            swRemove.Stop();

            double avgAddNs = swAdd.Elapsed.TotalMilliseconds * 1000000 / items;
            double avgRemoveNs = swRemove.Elapsed.TotalMilliseconds * 1000000 / items;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] ReactiveCollection ({items} items):");
            UnityEngine.Debug.Log($"  Add: {swAdd.ElapsedMilliseconds}ms total, {avgAddNs:F0}ns avg");
            UnityEngine.Debug.Log($"  Remove: {swRemove.ElapsedMilliseconds}ms total, {avgRemoveNs:F0}ns avg");
            UnityEngine.Debug.Log($"  Add notifications: {addCount}, Remove notifications: {removeCount}");

            Assert.Less(swAdd.ElapsedMilliseconds, 50, "Collection add too slow");
            Assert.Less(swRemove.ElapsedMilliseconds, 100, "Collection remove too slow");
        }

        [Test]
        public void Benchmark_ModuleInstaller_Lifecycle()
        {
            const int iterations = 1000;
            var builder = new ContainerBuilder();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var installer = new BenchmarkModuleInstaller();
                installer.Install(builder);
                installer.Initialize(_container);
                installer.Shutdown();
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Module Full Lifecycle ({iterations} cycles):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per full lifecycle");

            Assert.Less(sw.ElapsedMilliseconds, 200, "Module lifecycle too slow");
        }

        private class LocalService { }

        private class BenchmarkService : StradaService
        {
            protected override void OnInitialize() { }
        }

        private class BenchmarkController : StradaController
        {
            protected override void OnInitialize() { }
        }

        private class MultiPropertyModel : StradaModel
        {
            private readonly int _propertyCount;
            private ReactiveProperty<int>[] _properties;

            public MultiPropertyModel(int propertyCount)
            {
                _propertyCount = propertyCount;
            }

            protected override void OnInitialize()
            {
                _properties = new ReactiveProperty<int>[_propertyCount];
                for (int i = 0; i < _propertyCount; i++)
                {
                    _properties[i] = CreateProperty(i);
                }
            }
        }

        private class BenchmarkModuleInstaller : IModuleInstaller
        {
            public void Install(IContainerBuilder builder) { }
            public void Initialize(IContainer container) { }
            public void Shutdown() { }
        }
    }
}
