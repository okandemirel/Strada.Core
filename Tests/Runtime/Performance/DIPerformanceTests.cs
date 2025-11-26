using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.Performance
{
    // Simple service - no dependencies
    public class SimpleService
    {
        public int Value = 42;
    }

    // Complex dependency chain - 4 levels deep (realistic scenario)
    public class ServiceD { public int Value = 4; }
    public class ServiceC { public ServiceD D; public ServiceC(ServiceD d) => D = d; }
    public class ServiceB { public ServiceC C; public ServiceB(ServiceC c) => C = c; }
    public class ServiceA { public ServiceB B; public ServiceA(ServiceB b) => B = b; }

    // Wide dependency - multiple dependencies at same level
    public class DepOne { }
    public class DepTwo { }
    public class DepThree { }
    public class DepFour { }
    public class DepFive { }
    public class WideService
    {
        public DepOne One;
        public DepTwo Two;
        public DepThree Three;
        public DepFour Four;
        public DepFive Five;

        public WideService(DepOne one, DepTwo two, DepThree three, DepFour four, DepFive five)
        {
            One = one; Two = two; Three = three; Four = four; Five = five;
        }
    }

    // Interface-based registration
    public interface IRepository { }
    public class Repository : IRepository { }
    public interface IService { }
    public class Service : IService
    {
        public IRepository Repo;
        public Service(IRepository repo) => Repo = repo;
    }

    [TestFixture]
    [Category("Performance")]
    public class DIPerformanceTests
    {
        private const int WarmupIterations = 100;
        private const int SmallIterations = 10_000;
        private const int LargeIterations = 100_000;

        [SetUp]
        public void Setup()
        {
            // CRITICAL: Clear any DirectFactory delegates to ensure pure expression tree resolution
            ClearAllDirectFactories();
        }

        [TearDown]
        public void TearDown()
        {
            ClearAllDirectFactories();
        }

        private void ClearAllDirectFactories()
        {
            DirectFactory<SimpleService>.Delegate = null;
            DirectFactory<ServiceA>.Delegate = null;
            DirectFactory<ServiceB>.Delegate = null;
            DirectFactory<ServiceC>.Delegate = null;
            DirectFactory<ServiceD>.Delegate = null;
            DirectFactory<WideService>.Delegate = null;
            DirectFactory<DepOne>.Delegate = null;
            DirectFactory<DepTwo>.Delegate = null;
            DirectFactory<DepThree>.Delegate = null;
            DirectFactory<DepFour>.Delegate = null;
            DirectFactory<DepFive>.Delegate = null;
            DirectFactory<IRepository>.Delegate = null;
            DirectFactory<Repository>.Delegate = null;
            DirectFactory<IService>.Delegate = null;
            DirectFactory<Service>.Delegate = null;
        }

        [Test]
        public void Benchmark_Simple_Transient_10k()
        {
            var builder = new ContainerBuilder();
            builder.Register<SimpleService>(Lifetime.Transient);
            using var container = builder.Build();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
                container.Resolve<SimpleService>();

            // Verify correctness
            var instance = container.Resolve<SimpleService>();
            Assert.NotNull(instance);
            Assert.AreEqual(42, instance.Value);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < SmallIterations; i++)
                container.Resolve<SimpleService>();
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000 / SmallIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: Simple Transient ({SmallIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-op: {usPerOp:F3}μs");

            Assert.Less(usPerOp, 1.0, "Simple transient should resolve under 1μs");
        }

        [Test]
        public void Benchmark_DeepChain_Transient_10k()
        {
            var builder = new ContainerBuilder();
            builder.Register<ServiceD>(Lifetime.Transient);
            builder.Register<ServiceC>(Lifetime.Transient);
            builder.Register<ServiceB>(Lifetime.Transient);
            builder.Register<ServiceA>(Lifetime.Transient);
            using var container = builder.Build();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
                container.Resolve<ServiceA>();

            // Verify correctness - 4 levels deep
            var instance = container.Resolve<ServiceA>();
            Assert.NotNull(instance);
            Assert.NotNull(instance.B);
            Assert.NotNull(instance.B.C);
            Assert.NotNull(instance.B.C.D);
            Assert.AreEqual(4, instance.B.C.D.Value);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < SmallIterations; i++)
                container.Resolve<ServiceA>();
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000 / SmallIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: 4-Level Deep Chain ({SmallIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-op: {usPerOp:F3}μs");
            UnityEngine.Debug.Log($"  (Creates 4 objects per resolution)");

            Assert.Less(usPerOp, 5.0, "4-level deep transient should resolve under 5μs");
        }

        [Test]
        public void Benchmark_WideService_Transient_10k()
        {
            var builder = new ContainerBuilder();
            builder.Register<DepOne>(Lifetime.Transient);
            builder.Register<DepTwo>(Lifetime.Transient);
            builder.Register<DepThree>(Lifetime.Transient);
            builder.Register<DepFour>(Lifetime.Transient);
            builder.Register<DepFive>(Lifetime.Transient);
            builder.Register<WideService>(Lifetime.Transient);
            using var container = builder.Build();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
                container.Resolve<WideService>();

            // Verify correctness - 5 dependencies
            var instance = container.Resolve<WideService>();
            Assert.NotNull(instance);
            Assert.NotNull(instance.One);
            Assert.NotNull(instance.Two);
            Assert.NotNull(instance.Three);
            Assert.NotNull(instance.Four);
            Assert.NotNull(instance.Five);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < SmallIterations; i++)
                container.Resolve<WideService>();
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000 / SmallIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: Wide Service 5 Deps ({SmallIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-op: {usPerOp:F3}μs");
            UnityEngine.Debug.Log($"  (Creates 6 objects per resolution)");

            Assert.Less(usPerOp, 5.0, "Wide service should resolve under 5μs");
        }

        [Test]
        public void Benchmark_Singleton_100k()
        {
            var builder = new ContainerBuilder();
            builder.Register<ServiceD>(Lifetime.Singleton);
            builder.Register<ServiceC>(Lifetime.Singleton);
            builder.Register<ServiceB>(Lifetime.Singleton);
            builder.Register<ServiceA>(Lifetime.Singleton);
            using var container = builder.Build();

            // Warmup and trigger singleton creation
            for (int i = 0; i < WarmupIterations; i++)
                container.Resolve<ServiceA>();

            // Benchmark cached singleton lookup
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < LargeIterations; i++)
                container.Resolve<ServiceA>();
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000 / LargeIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: Singleton Lookup ({LargeIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-op: {usPerOp:F4}μs ({usPerOp * 1000:F2}ns)");

            Assert.Less(usPerOp, 0.5, "Singleton lookup should be under 0.5μs (500ns)");
        }

        [Test]
        public void Benchmark_Interface_Registration_10k()
        {
            var builder = new ContainerBuilder();
            builder.Register<IRepository, Repository>(Lifetime.Transient);
            builder.Register<IService, Service>(Lifetime.Transient);
            using var container = builder.Build();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
                container.Resolve<IService>();

            // Verify correctness
            var instance = container.Resolve<IService>();
            Assert.NotNull(instance);
            Assert.IsInstanceOf<Service>(instance);
            Assert.NotNull(((Service)instance).Repo);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < SmallIterations; i++)
                container.Resolve<IService>();
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000 / SmallIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: Interface Registration ({SmallIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-op: {usPerOp:F3}μs");

            Assert.Less(usPerOp, 3.0, "Interface resolution should be under 3μs");
        }

        [Test]
        public void Benchmark_ContainerBuild_100Types()
        {
            const int typeCount = 100;

            var sw = Stopwatch.StartNew();
            var builder = new ContainerBuilder();

            // Register 100 different factory registrations
            for (int i = 0; i < typeCount; i++)
            {
                builder.RegisterFactory<SimpleService>(_ => new SimpleService());
            }

            var container = builder.Build();
            sw.Stop();
            container.Dispose();

            UnityEngine.Debug.Log($"=== STRADA DI: Container Build ({typeCount} registrations) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-registration: {sw.Elapsed.TotalMilliseconds * 1000 / typeCount:F2}μs");

            Assert.Less(sw.ElapsedMilliseconds, 50, "100 registrations should build under 50ms");
        }

        [Test]
        public void Benchmark_ContainerBuild_1000Types()
        {
            const int typeCount = 1000;

            var sw = Stopwatch.StartNew();
            var builder = new ContainerBuilder();

            for (int i = 0; i < typeCount; i++)
            {
                builder.RegisterFactory<SimpleService>(_ => new SimpleService());
            }

            var container = builder.Build();
            sw.Stop();
            container.Dispose();

            UnityEngine.Debug.Log($"=== STRADA DI: Container Build ({typeCount} registrations) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-registration: {sw.Elapsed.TotalMilliseconds * 1000 / typeCount:F2}μs");

            Assert.Less(sw.ElapsedMilliseconds, 200, "1000 registrations should build under 200ms");
        }

        [Test]
        public void Benchmark_ScopedResolution_10k()
        {
            var builder = new ContainerBuilder();
            builder.Register<ServiceD>(Lifetime.Scoped);
            builder.Register<ServiceC>(Lifetime.Scoped);
            builder.Register<ServiceB>(Lifetime.Scoped);
            builder.Register<ServiceA>(Lifetime.Scoped);
            using var container = builder.Build();
            using var scope = container.CreateScope();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
                scope.Resolve<ServiceA>();

            // Verify scoped behavior - same instance within scope
            var first = scope.Resolve<ServiceA>();
            var second = scope.Resolve<ServiceA>();
            Assert.AreSame(first, second, "Scoped should return same instance");

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < SmallIterations; i++)
                scope.Resolve<ServiceA>();
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000 / SmallIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: Scoped Resolution ({SmallIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-op: {usPerOp:F4}μs ({usPerOp * 1000:F2}ns)");

            Assert.Less(usPerOp, 0.5, "Scoped lookup should be under 0.5μs");
        }

        [Test]
        public void Benchmark_ScopeCreation_1k()
        {
            var builder = new ContainerBuilder();
            builder.Register<ServiceD>(Lifetime.Scoped);
            builder.Register<ServiceC>(Lifetime.Scoped);
            builder.Register<ServiceB>(Lifetime.Scoped);
            builder.Register<ServiceA>(Lifetime.Scoped);
            using var container = builder.Build();

            const int iterations = 1000;

            // Warmup
            for (int i = 0; i < 10; i++)
            {
                using var scope = container.CreateScope();
                scope.Resolve<ServiceA>();
            }

            // Benchmark scope creation + first resolution
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                using var scope = container.CreateScope();
                var _ = scope.Resolve<ServiceA>();
            }
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000 / iterations;

            UnityEngine.Debug.Log($"=== STRADA DI: Scope Creation + Resolve ({iterations:N0} cycles) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-cycle: {usPerOp:F2}μs");

            Assert.Less(usPerOp, 10, "Scope creation + resolve should be under 10μs");
        }

        [Test]
        public void Benchmark_MixedLifetimes_10k()
        {
            var builder = new ContainerBuilder();
            builder.Register<ServiceD>(Lifetime.Singleton);   // Singleton at bottom
            builder.Register<ServiceC>(Lifetime.Transient);   // Transient middle
            builder.Register<ServiceB>(Lifetime.Transient);   // Transient middle
            builder.Register<ServiceA>(Lifetime.Transient);   // Transient top
            using var container = builder.Build();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
                container.Resolve<ServiceA>();

            // Verify mixed behavior
            var first = container.Resolve<ServiceA>();
            var second = container.Resolve<ServiceA>();
            Assert.AreNotSame(first, second, "Top should be transient");
            Assert.AreSame(first.B.C.D, second.B.C.D, "Bottom should be singleton");

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < SmallIterations; i++)
                container.Resolve<ServiceA>();
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000 / SmallIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: Mixed Lifetimes ({SmallIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total: {sw.Elapsed.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Per-op: {usPerOp:F3}μs");
            UnityEngine.Debug.Log($"  (1 singleton lookup + 3 transient creates)");

            Assert.Less(usPerOp, 3.0, "Mixed lifetime should resolve under 3μs");
        }

        [Test]
        public void Benchmark_GCAllocation_Transient()
        {
            var builder = new ContainerBuilder();
            builder.Register<ServiceD>(Lifetime.Transient);
            builder.Register<ServiceC>(Lifetime.Transient);
            builder.Register<ServiceB>(Lifetime.Transient);
            builder.Register<ServiceA>(Lifetime.Transient);
            using var container = builder.Build();

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
                container.Resolve<ServiceA>();

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < SmallIterations; i++)
                container.Resolve<ServiceA>();

            long memAfter = GC.GetTotalMemory(true);
            long allocated = memAfter - memBefore;
            double bytesPerOp = (double)allocated / SmallIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: GC Allocation ({SmallIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total allocated: {allocated / 1024.0:F2}KB");
            UnityEngine.Debug.Log($"  Per-op: {bytesPerOp:F1} bytes");
            UnityEngine.Debug.Log($"  (Expected: ~96 bytes for 4 objects)");

            // Each resolution creates 4 objects, minimum ~24 bytes each = 96 bytes
            // Allow some overhead for object headers
            Assert.Less(bytesPerOp, 200, "Should allocate less than 200 bytes per resolution");
        }

        [Test]
        public void Benchmark_GCAllocation_Singleton()
        {
            var builder = new ContainerBuilder();
            builder.Register<ServiceD>(Lifetime.Singleton);
            builder.Register<ServiceC>(Lifetime.Singleton);
            builder.Register<ServiceB>(Lifetime.Singleton);
            builder.Register<ServiceA>(Lifetime.Singleton);
            using var container = builder.Build();

            // Warmup - creates the singletons
            container.Resolve<ServiceA>();

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < LargeIterations; i++)
                container.Resolve<ServiceA>();

            long memAfter = GC.GetTotalMemory(true);
            long allocated = memAfter - memBefore;
            double bytesPerOp = (double)allocated / LargeIterations;

            UnityEngine.Debug.Log($"=== STRADA DI: GC Allocation Singleton ({LargeIterations:N0} resolutions) ===");
            UnityEngine.Debug.Log($"  Total allocated: {allocated / 1024.0:F2}KB");
            UnityEngine.Debug.Log($"  Per-op: {bytesPerOp:F2} bytes");
            UnityEngine.Debug.Log($"  (Expected: ~0 bytes - cached lookup)");

            // Singleton lookup should allocate nothing
            Assert.Less(bytesPerOp, 1, "Singleton should allocate near-zero per resolution");
        }

        [Test]
        public void Benchmark_Comparison_ManualVsDI()
        {
            var builder = new ContainerBuilder();
            builder.Register<ServiceD>(Lifetime.Transient);
            builder.Register<ServiceC>(Lifetime.Transient);
            builder.Register<ServiceB>(Lifetime.Transient);
            builder.Register<ServiceA>(Lifetime.Transient);
            using var container = builder.Build();

            // Warmup both
            for (int i = 0; i < WarmupIterations; i++)
            {
                container.Resolve<ServiceA>();
                new ServiceA(new ServiceB(new ServiceC(new ServiceD())));
            }

            // Benchmark manual construction
            var swManual = Stopwatch.StartNew();
            for (int i = 0; i < SmallIterations; i++)
            {
                var instance = new ServiceA(new ServiceB(new ServiceC(new ServiceD())));
            }
            swManual.Stop();

            // Benchmark DI
            var swDI = Stopwatch.StartNew();
            for (int i = 0; i < SmallIterations; i++)
            {
                var instance = container.Resolve<ServiceA>();
            }
            swDI.Stop();

            double manualUs = swManual.Elapsed.TotalMilliseconds * 1000 / SmallIterations;
            double diUs = swDI.Elapsed.TotalMilliseconds * 1000 / SmallIterations;
            double overhead = diUs / manualUs;

            UnityEngine.Debug.Log($"=== STRADA DI vs Manual Construction ({SmallIterations:N0} iterations) ===");
            UnityEngine.Debug.Log($"  Manual new(): {manualUs:F4}μs/op");
            UnityEngine.Debug.Log($"  DI Resolve(): {diUs:F4}μs/op");
            UnityEngine.Debug.Log($"  DI Overhead: {overhead:F2}x slower than manual");
            UnityEngine.Debug.Log($"  (Typical DI overhead is 2-10x)");

            // DI should be no more than 20x slower than manual construction
            Assert.Less(overhead, 20, "DI overhead should be less than 20x");
        }
    }
}
