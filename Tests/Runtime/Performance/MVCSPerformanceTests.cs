using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.Modules;
using Strada.Core.MVCS;

namespace Strada.Core.Tests.Tests.Runtime.Performance
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
            builder.Register<MessageBus>(Lifetime.Singleton);
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
            const int Iterations = 10000;
            const int Warmup = 100;

            for (int i = 0; i < Warmup; i++)
            {
                var service = new BenchmarkService();
                InjectionProcessor.Inject(service, _container);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                var service = new BenchmarkService();
                InjectionProcessor.Inject(service, _container);
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / Iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] InjectionProcessor ({Iterations} injections):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per injection");

            Assert.Less(sw.ElapsedMilliseconds, 100, "Injection too slow (Target: <100ms for 10k)");
        }

        [Test]
        public void Benchmark_ReactiveProperty_100k_Updates()
        {
            const int Iterations = 100000;
            const int Warmup = 1000;

            var property = new ReactiveProperty<int>(0);
            int notifyCount = 0;
            property.Subscribe(_ => notifyCount++);

            for (int i = 0; i < Warmup; i++)
                property.Value = i;

            notifyCount = 0;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                property.Value = i;
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / Iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] ReactiveProperty Updates ({Iterations} updates):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Notifications: {notifyCount}");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per update");

            Assert.Less(sw.ElapsedMilliseconds, 50, "ReactiveProperty updates too slow");
        }

        [Test]
        public void Benchmark_ReactiveProperty_MultipleSubscribers()
        {
            const int Subscribers = 10;
            const int Iterations = 10000;

            var property = new ReactiveProperty<int>(0);
            int[] counts = new int[Subscribers];

            for (int s = 0; s < Subscribers; s++)
            {
                int idx = s;
                property.Subscribe(_ => counts[idx]++);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                property.Value = i;
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / Iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] ReactiveProperty with {Subscribers} subscribers ({Iterations} updates):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per update (dispatching to {Subscribers} subscribers)");

            Assert.Less(sw.ElapsedMilliseconds, 100, "ReactiveProperty multi-subscriber too slow");
        }

        [Test]
        public void Benchmark_ContainerScope_10k_Resolutions()
        {
            const int Iterations = 10000;
            const int Warmup = 100;

            var builder = new ContainerBuilder();
            builder.Register<LocalService>(Lifetime.Singleton);
            var scopedContainer = builder.Build();

            for (int i = 0; i < Warmup; i++)
                scopedContainer.Resolve<LocalService>();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                var svc = scopedContainer.Resolve<LocalService>();
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / Iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Container Resolution ({Iterations} resolutions):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per resolution");

            scopedContainer.Dispose();

            Assert.Less(sw.ElapsedMilliseconds, 10, "Container resolution too slow");
        }

        [Test]
        public void Benchmark_ContainerScope_NestedScopes()
        {
            const int Depth = 10;
            const int Iterations = 10000;

            var builder = new ContainerBuilder();
            builder.Register<LocalService>(Lifetime.Scoped);
            var rootContainer = builder.Build();

            IContainerScope current = rootContainer.CreateScope();
            for (int d = 0; d < Depth - 1; d++)
            {
                current = current.CreateScope();
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                var svc = current.Resolve<LocalService>();
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / Iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Nested Scope ({Depth} levels, {Iterations} resolutions):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per resolution");

            ((IDisposable)current).Dispose();
            rootContainer.Dispose();

            Assert.Less(sw.ElapsedMilliseconds, 20, "Nested scope resolution too slow");
        }

        [Test]
        public void Benchmark_Controller_Lifecycle()
        {
            const int Iterations = 1000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                var controller = new BenchmarkController();
                InjectionProcessor.Inject(controller, _container);
                controller.Initialize();
                controller.Dispose();
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / Iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Controller Full Lifecycle ({Iterations} cycles):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per cycle (create+inject+init+dispose)");

            Assert.Less(sw.ElapsedMilliseconds, 500, "Controller lifecycle too slow");
        }

        [Test]
        public void Benchmark_Model_PropertyCreation()
        {
            const int Properties = 100;
            const int Iterations = 1000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                var model = new MultiPropertyModel(Properties);
                model.Initialize();
                model.Dispose();
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / Iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Model with {Properties} Properties ({Iterations} cycles):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per cycle");

            Assert.Less(sw.ElapsedMilliseconds, 1000, "Model property creation too slow");
        }

        [Test]
        public void Benchmark_ReactiveCollection_Operations()
        {
            const int Items = 10000;
            const int Warmup = 100;

            var collection = new ReactiveCollection<int>();
            int addCount = 0;
            int removeCount = 0;

            collection.OnAdd(_ => addCount++);
            collection.OnRemove(_ => removeCount++);

            for (int i = 0; i < Warmup; i++)
            {
                collection.Add(i);
                collection.Remove(i);
            }
            collection.Clear();
            addCount = 0;
            removeCount = 0;

            var swAdd = Stopwatch.StartNew();
            for (int i = 0; i < Items; i++)
            {
                collection.Add(i);
            }
            swAdd.Stop();

            var swRemove = Stopwatch.StartNew();
            for (int i = Items - 1; i >= 0; i--)
            {
                collection.Remove(i);
            }
            swRemove.Stop();

            double avgAddNs = swAdd.Elapsed.TotalMilliseconds * 1000000 / Items;
            double avgRemoveNs = swRemove.Elapsed.TotalMilliseconds * 1000000 / Items;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] ReactiveCollection ({Items} items):");
            UnityEngine.Debug.Log($"  Add: {swAdd.ElapsedMilliseconds}ms total, {avgAddNs:F0}ns avg");
            UnityEngine.Debug.Log($"  Remove: {swRemove.ElapsedMilliseconds}ms total, {avgRemoveNs:F0}ns avg");
            UnityEngine.Debug.Log($"  Add notifications: {addCount}, Remove notifications: {removeCount}");

            Assert.Less(swAdd.ElapsedMilliseconds, 50, "Collection add too slow");
            Assert.Less(swRemove.ElapsedMilliseconds, 100, "Collection remove too slow");
        }

        [Test]
        public void Benchmark_ModuleInstaller_Lifecycle()
        {
            const int Iterations = 1000;
            var builder = new ContainerBuilder();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                var installer = new BenchmarkModuleInstaller();
                installer.Install(builder);
                installer.Initialize(_container);
                installer.Shutdown();
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / Iterations;

            UnityEngine.Debug.Log($"[MVCS BENCHMARK] Module Full Lifecycle ({Iterations} cycles):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}μs per full lifecycle");

            Assert.Less(sw.ElapsedMilliseconds, 200, "Module lifecycle too slow");
        }

        private class LocalService { }

        private class BenchmarkService : StradaService
        {
            protected override void OnInitialize() { }
        }

        private class BenchmarkController : Controller
        {
            protected override void OnInitialize() { }
        }

        private class MultiPropertyModel : Model
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
