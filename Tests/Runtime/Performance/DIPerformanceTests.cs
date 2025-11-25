using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.DI;
using Strada.Core.DI.Attributes;

namespace Strada.Core.Tests.Tests.Runtime.Performance
{
    [StradaService(ServiceLifetime.Transient)]
    public class Level4
    {
        public int Value = 4;
    }

    [StradaService(ServiceLifetime.Transient)]
    public class Level3
    {
        public Level4 Dependency;
        public Level3(Level4 dep) { Dependency = dep; }
    }

    [StradaService(ServiceLifetime.Transient)]
    public class Level2
    {
        public Level3 Dependency;
        public Level2(Level3 dep) { Dependency = dep; }
    }

    [StradaService(ServiceLifetime.Transient)]
    public class Level1
    {
        public Level2 Dependency;
        public Level1(Level2 dep) { Dependency = dep; }
    }

    [TestFixture]
    [Category("Performance")]
    public class DIPerformanceTests
    {
        private IContainer _container;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            DirectFactory<Level4>.Delegate = _ => new Level4();
            DirectFactory<Level3>.Delegate = _ => new Level3(new Level4());
            DirectFactory<Level2>.Delegate = _ => new Level2(new Level3(new Level4()));
            DirectFactory<Level1>.Delegate = _ => new Level1(new Level2(new Level3(new Level4())));
        }

        [SetUp]
        public void Setup()
        {
            var builder = new ContainerBuilder();
            builder.Register<Level4>(Lifetime.Transient);
            builder.Register<Level3>(Lifetime.Transient);
            builder.Register<Level2>(Lifetime.Transient);
            builder.Register<Level1>(Lifetime.Transient);
            _container = builder.Build();
        }

        [Test]
        public void Benchmark_10k_Transient_Resolutions()
        {
            const int iterations = 10000;

            UnityEngine.Debug.Log($"DirectFactory<Level1>.Delegate is null: {DirectFactory<Level1>.Delegate == null}");

            // Warmup
            for (int i = 0; i < 100; i++) _container.Resolve<Level1>();

            // Validation (ensure correctness before benchmarking)
            var checkInstance = _container.Resolve<Level1>();
            Assert.NotNull(checkInstance);
            Assert.NotNull(checkInstance.Dependency);
            Assert.NotNull(checkInstance.Dependency.Dependency);
            Assert.NotNull(checkInstance.Dependency.Dependency.Dependency);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var instance = _container.Resolve<Level1>();
            }
            sw.Stop();

            long memoryBefore = GC.GetTotalMemory(true);
            for (int i = 0; i < iterations; i++)
            {
                var instance = _container.Resolve<Level1>();
            }
            long memoryAfter = GC.GetTotalMemory(true);
            long gcAllocation = (memoryAfter - memoryBefore) / 1024;

            UnityEngine.Debug.Log($"[STRADA BENCHMARK] 10k Transient Resolutions (4-level deep):");
            UnityEngine.Debug.Log($"  Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  GC: {gcAllocation}KB");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds / iterations:F4}ms per resolution");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1000 / iterations:F2}μs per resolution");

            Assert.Less(sw.ElapsedMilliseconds, 200, "Transient resolution too slow");
        }

        [Test]
        public void Benchmark_Singleton_Resolution()
        {
            var builder = new ContainerBuilder();
            builder.Register<Level4>(Lifetime.Singleton);
            builder.Register<Level3>(Lifetime.Singleton);
            builder.Register<Level2>(Lifetime.Singleton);
            builder.Register<Level1>(Lifetime.Singleton);
            var container = builder.Build();

            const int iterations = 100000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var instance = container.Resolve<Level1>();
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA BENCHMARK] 100k Singleton Resolutions:");
            UnityEngine.Debug.Log($"  Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds * 1000 / iterations:F4}μs per resolution");

            Assert.Less(sw.ElapsedMilliseconds, 10, "Singleton resolution too slow (Target: <10ms for 100k)");
        }

        [Test]
        public void Benchmark_Container_Build()
        {
            const int typeCount = 1000;

            var sw = Stopwatch.StartNew();
            var builder = new ContainerBuilder();

            for (int i = 0; i < typeCount; i++)
            {
                builder.RegisterFactory<Level1>(_ => new Level1(new Level2(new Level3(new Level4()))));
            }

            var container = builder.Build();
            sw.Stop();

            UnityEngine.Debug.Log($"[STRADA BENCHMARK] Container Build ({typeCount} registrations):");
            UnityEngine.Debug.Log($"  Time: {sw.ElapsedMilliseconds}ms");

            Assert.Less(sw.ElapsedMilliseconds, 100, "Container build too slow");
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
        }

        [Test]
        public void Benchmark_SourceGen_vs_ExpressionTree()
        {
            const int iterations = 10000;
            const int warmupIterations = 100;

            DirectFactory<Level4>.Delegate = _ => new Level4();
            DirectFactory<Level3>.Delegate = _ => new Level3(new Level4());
            DirectFactory<Level2>.Delegate = _ => new Level2(new Level3(new Level4()));
            DirectFactory<Level1>.Delegate = _ => new Level1(new Level2(new Level3(new Level4())));

            var builder1 = new ContainerBuilder();
            builder1.Register<Level4>(Lifetime.Transient);
            builder1.Register<Level3>(Lifetime.Transient);
            builder1.Register<Level2>(Lifetime.Transient);
            builder1.Register<Level1>(Lifetime.Transient);
            var sourceGenContainer = builder1.Build();

            for (int i = 0; i < warmupIterations; i++)
                sourceGenContainer.Resolve<Level1>();

            var swSourceGen = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                sourceGenContainer.Resolve<Level1>();
            swSourceGen.Stop();
            var sourceGenTime = swSourceGen.Elapsed.TotalMilliseconds;

            sourceGenContainer.Dispose();

            DirectFactory<Level4>.Delegate = null;
            DirectFactory<Level3>.Delegate = null;
            DirectFactory<Level2>.Delegate = null;
            DirectFactory<Level1>.Delegate = null;

            var builder2 = new ContainerBuilder();
            builder2.Register<Level4>(Lifetime.Transient);
            builder2.Register<Level3>(Lifetime.Transient);
            builder2.Register<Level2>(Lifetime.Transient);
            builder2.Register<Level1>(Lifetime.Transient);
            var exprTreeContainer = builder2.Build();

            for (int i = 0; i < warmupIterations; i++)
                exprTreeContainer.Resolve<Level1>();

            var swExprTree = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                exprTreeContainer.Resolve<Level1>();
            swExprTree.Stop();
            var exprTreeTime = swExprTree.Elapsed.TotalMilliseconds;

            exprTreeContainer.Dispose();

            double sourceGenPerOp = sourceGenTime * 1000 / iterations;
            double exprTreePerOp = exprTreeTime * 1000 / iterations;
            double speedup = exprTreePerOp / sourceGenPerOp;

            UnityEngine.Debug.Log($"[DI COMPARISON] Source-Gen vs Expression Tree ({iterations} resolutions):");
            UnityEngine.Debug.Log($"  Source-Gen:      {sourceGenTime:F2}ms total, {sourceGenPerOp:F2}μs/op");
            UnityEngine.Debug.Log($"  Expression-Tree: {exprTreeTime:F2}ms total, {exprTreePerOp:F2}μs/op");
            UnityEngine.Debug.Log($"  Speedup:         {speedup:F2}x faster with Source-Gen");

            Assert.Less(sourceGenPerOp, 10, "Source-gen should resolve under 10μs");
        }
    }
}
