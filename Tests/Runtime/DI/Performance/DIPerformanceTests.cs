using NUnit.Framework;
using System;
using System.Diagnostics;
using Strada.Core.DI;

namespace Strada.Core.Tests.DI.Performance
{
    /// <summary>
    /// Performance benchmarks for the Strada DI container.
    /// Performance Targets: Resolution &lt;9ms (Mono), &lt;0.9ms (IL2CPP) for 10k resolutions,
    /// GC Allocation &lt;50KB (Mono), &lt;100KB (IL2CPP), Zero allocation on cached singleton paths.
    /// Comparison Target: Beat Reflex DI by 10-20%.
    /// </summary>
    [TestFixture]
    public class DIPerformanceTests
    {
        private const int ITERATIONS_10K = 10000;
        private const int ITERATIONS_1K = 1000;
        private const int WARMUP_ITERATIONS = 100;

        #region Test Interfaces and Classes

        public interface ISimpleService { }
        public class SimpleService : ISimpleService { }

        public interface ISingleDependency { }
        public class SingleDependency : ISingleDependency
        {
            public ISingleDependency Nested { get; }
            public SingleDependency(ISimpleService service) { }
        }

        public interface IComplexService { }
        public class ComplexService : IComplexService
        {
            public ComplexService(ISimpleService simple, ISingleDependency single, IComplexService2 complex) { }
        }

        public interface IComplexService2 { }
        public class ComplexService2 : IComplexService2
        {
            public ComplexService2(ISimpleService simple) { }
        }

        public interface ILevel1 { }
        public interface ILevel2 { }
        public interface ILevel3 { }
        public interface ILevel4 { }
        public interface ILevel5 { }

        public class Level1 : ILevel1 { public Level1(ILevel2 level2) { } }
        public class Level2 : ILevel2 { public Level2(ILevel3 level3) { } }
        public class Level3 : ILevel3 { public Level3(ILevel4 level4) { } }
        public class Level4 : ILevel4 { public Level4(ILevel5 level5) { } }
        public class Level5 : ILevel5 { }

        public interface IDisposableService : IDisposable { }
        public class DisposableService : IDisposableService
        {
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }

        #endregion

        #region Helper Methods

        private void Warmup(Action action)
        {
            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                action();
            }
        }

        private BenchmarkResult Benchmark(string name, Action action, int iterations)
        {
            Warmup(action);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memoryBefore = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();

            action();

            stopwatch.Stop();
            long memoryAfter = GC.GetTotalMemory(false);
            long memoryAllocated = Math.Max(0, memoryAfter - memoryBefore);

            var result = new BenchmarkResult
            {
                Name = name,
                Iterations = iterations,
                TotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                MemoryAllocatedBytes = memoryAllocated
            };

            LogBenchmarkResult(result);
            return result;
        }

        private void LogBenchmarkResult(BenchmarkResult result)
        {
            UnityEngine.Debug.Log($"[BENCHMARK] {result.Name}");
            UnityEngine.Debug.Log($"  Iterations: {result.Iterations:N0}");
            UnityEngine.Debug.Log($"  Total Time: {result.TotalMilliseconds:F2}ms");
            UnityEngine.Debug.Log($"  Avg Time: {result.AverageMilliseconds:F4}ms");
            UnityEngine.Debug.Log($"  Memory: {result.MemoryAllocatedKB:F2}KB ({result.MemoryAllocatedBytes:N0} bytes)");
            UnityEngine.Debug.Log($"  Per-Op Memory: {result.MemoryPerOperation:F2} bytes");
        }

        #endregion

        #region Singleton Resolution Benchmarks

        [Test]
        public void Benchmark_SingletonResolution_10k_Parameterless()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
            var container = builder.Build();

            var result = Benchmark(
                "Singleton Resolution (10k) - Parameterless Constructor",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_10K; i++)
                    {
                        var service = container.Resolve<ISimpleService>();
                    }
                },
                ITERATIONS_10K
            );

            Assert.Less(result.TotalMilliseconds, 9.0,
                $"Singleton resolution (10k) should be <9ms on Mono, got {result.TotalMilliseconds:F2}ms");

            Assert.Less(result.MemoryAllocatedKB, 1.0,
                $"Cached singleton resolution should allocate <1KB, got {result.MemoryAllocatedKB:F2}KB");
        }

        [Test]
        public void Benchmark_SingletonResolution_10k_WithDependencies()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
            builder.Register<ISingleDependency, SingleDependency>(Lifetime.Singleton);
            var container = builder.Build();

            var result = Benchmark(
                "Singleton Resolution (10k) - With Dependencies",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_10K; i++)
                    {
                        var service = container.Resolve<ISingleDependency>();
                    }
                },
                ITERATIONS_10K
            );

            Assert.Less(result.TotalMilliseconds, 9.0,
                $"Singleton resolution with deps (10k) should be <9ms, got {result.TotalMilliseconds:F2}ms");
            Assert.Less(result.MemoryAllocatedKB, 1.0,
                $"Cached singleton resolution should allocate <1KB, got {result.MemoryAllocatedKB:F2}KB");
        }

        [Test]
        public void Benchmark_SingletonResolution_10k_ComplexDependencies()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
            builder.Register<ISingleDependency, SingleDependency>(Lifetime.Singleton);
            builder.Register<IComplexService2, ComplexService2>(Lifetime.Singleton);
            builder.Register<IComplexService, ComplexService>(Lifetime.Singleton);
            var container = builder.Build();

            var result = Benchmark(
                "Singleton Resolution (10k) - Complex Dependencies (3 deps)",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_10K; i++)
                    {
                        var service = container.Resolve<IComplexService>();
                    }
                },
                ITERATIONS_10K
            );

            Assert.Less(result.TotalMilliseconds, 9.0,
                $"Singleton resolution complex (10k) should be <9ms, got {result.TotalMilliseconds:F2}ms");
        }

        [Test]
        public void Benchmark_SingletonResolution_10k_DeepDependencyChain()
        {
            var builder = new ContainerBuilder();
            builder.Register<ILevel5, Level5>(Lifetime.Singleton);
            builder.Register<ILevel4, Level4>(Lifetime.Singleton);
            builder.Register<ILevel3, Level3>(Lifetime.Singleton);
            builder.Register<ILevel2, Level2>(Lifetime.Singleton);
            builder.Register<ILevel1, Level1>(Lifetime.Singleton);
            var container = builder.Build();

            var result = Benchmark(
                "Singleton Resolution (10k) - Deep Dependency Chain (5 levels)",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_10K; i++)
                    {
                        var service = container.Resolve<ILevel1>();
                    }
                },
                ITERATIONS_10K
            );

            Assert.Less(result.TotalMilliseconds, 9.0,
                $"Singleton resolution deep chain (10k) should be <9ms, got {result.TotalMilliseconds:F2}ms");
        }

        #endregion

        #region Transient Resolution Benchmarks

        [Test]
        public void Benchmark_TransientResolution_1k_Parameterless()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Transient);
            var container = builder.Build();

            var result = Benchmark(
                "Transient Resolution (1k) - Parameterless Constructor",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_1K; i++)
                    {
                        var service = container.Resolve<ISimpleService>();
                    }
                },
                ITERATIONS_1K
            );

            Assert.Less(result.TotalMilliseconds, 5.0,
                $"Transient resolution (1k) should be <5ms, got {result.TotalMilliseconds:F2}ms");
        }

        [Test]
        public void Benchmark_TransientResolution_1k_WithDependencies()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
            builder.Register<ISingleDependency, SingleDependency>(Lifetime.Transient);
            var container = builder.Build();

            var result = Benchmark(
                "Transient Resolution (1k) - With Dependencies",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_1K; i++)
                    {
                        var service = container.Resolve<ISingleDependency>();
                    }
                },
                ITERATIONS_1K
            );

            Assert.Less(result.TotalMilliseconds, 5.0,
                $"Transient resolution with deps (1k) should be <5ms, got {result.TotalMilliseconds:F2}ms");
        }

        #endregion

        #region Factory Benchmarks

        [Test]
        public void Benchmark_FactoryResolution_10k_Singleton()
        {
            var builder = new ContainerBuilder();
            builder.RegisterFactory<ISimpleService>(c => new SimpleService(), Lifetime.Singleton);
            var container = builder.Build();

            var result = Benchmark(
                "Factory Resolution (10k) - Singleton",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_10K; i++)
                    {
                        var service = container.Resolve<ISimpleService>();
                    }
                },
                ITERATIONS_10K
            );

            Assert.Less(result.TotalMilliseconds, 9.0,
                $"Factory singleton resolution (10k) should be <9ms, got {result.TotalMilliseconds:F2}ms");
        }

        [Test]
        public void Benchmark_FactoryResolution_1k_Transient()
        {
            var builder = new ContainerBuilder();
            builder.RegisterFactory<ISimpleService>(c => new SimpleService(), Lifetime.Transient);
            var container = builder.Build();

            var result = Benchmark(
                "Factory Resolution (1k) - Transient",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_1K; i++)
                    {
                        var service = container.Resolve<ISimpleService>();
                    }
                },
                ITERATIONS_1K
            );

            Assert.Less(result.TotalMilliseconds, 5.0,
                $"Factory transient resolution (1k) should be <5ms, got {result.TotalMilliseconds:F2}ms");
        }

        #endregion

        #region Scope Benchmarks

        [Test]
        public void Benchmark_ScopeCreation_1k()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
            var container = builder.Build();

            var result = Benchmark(
                "Scope Creation (1k)",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_1K; i++)
                    {
                        using var scope = container.CreateScope();
                    }
                },
                ITERATIONS_1K
            );

            Assert.Less(result.TotalMilliseconds, 10.0,
                $"Scope creation (1k) should be <10ms, got {result.TotalMilliseconds:F2}ms");
        }

        [Test]
        public void Benchmark_ScopedResolution_1k()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Scoped);
            var container = builder.Build();

            var result = Benchmark(
                "Scoped Resolution (1k) - First Resolution Per Scope",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_1K; i++)
                    {
                        using var scope = container.CreateScope();
                        var service = scope.Resolve<ISimpleService>();
                    }
                },
                ITERATIONS_1K
            );

            Assert.Less(result.TotalMilliseconds, 15.0,
                $"Scoped resolution (1k) should be <15ms, got {result.TotalMilliseconds:F2}ms");
        }

        [Test]
        public void Benchmark_ScopedResolution_CachedInScope_10k()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            var result = Benchmark(
                "Scoped Resolution (10k) - Cached Within Scope",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_10K; i++)
                    {
                        var service = scope.Resolve<ISimpleService>();
                    }
                },
                ITERATIONS_10K
            );

            scope.Dispose();

            Assert.Less(result.TotalMilliseconds, 9.0,
                $"Cached scoped resolution (10k) should be <9ms, got {result.TotalMilliseconds:F2}ms");
            Assert.Less(result.MemoryAllocatedKB, 1.0,
                $"Cached scoped resolution should allocate <1KB, got {result.MemoryAllocatedKB:F2}KB");
        }

        #endregion

        #region Container Build Benchmarks

        [Test]
        public void Benchmark_ContainerBuild_Simple()
        {
            var result = Benchmark(
                "Container Build - Simple (5 registrations)",
                () =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
                    builder.Register<ISingleDependency, SingleDependency>(Lifetime.Singleton);
                    builder.Register<IComplexService2, ComplexService2>(Lifetime.Singleton);
                    builder.Register<IComplexService, ComplexService>(Lifetime.Singleton);
                    builder.Register<IDisposableService, DisposableService>(Lifetime.Singleton);
                    var container = builder.Build();
                },
                100
            );

            Assert.Less(result.TotalMilliseconds, 50.0,
                $"Container build (100x) should be <50ms, got {result.TotalMilliseconds:F2}ms");
        }

        [Test]
        public void Benchmark_ContainerBuild_Complex()
        {
            var result = Benchmark(
                "Container Build - Complex (10 registrations with deep chain)",
                () =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
                    builder.Register<ISingleDependency, SingleDependency>(Lifetime.Transient);
                    builder.Register<IComplexService2, ComplexService2>(Lifetime.Singleton);
                    builder.Register<IComplexService, ComplexService>(Lifetime.Singleton);
                    builder.Register<ILevel5, Level5>(Lifetime.Singleton);
                    builder.Register<ILevel4, Level4>(Lifetime.Singleton);
                    builder.Register<ILevel3, Level3>(Lifetime.Singleton);
                    builder.Register<ILevel2, Level2>(Lifetime.Singleton);
                    builder.Register<ILevel1, Level1>(Lifetime.Singleton);
                    builder.Register<IDisposableService, DisposableService>(Lifetime.Singleton);
                    var container = builder.Build();
                },
                100
            );

            Assert.Less(result.TotalMilliseconds, 100.0,
                $"Container build complex (100x) should be <100ms, got {result.TotalMilliseconds:F2}ms");
        }

        #endregion

        #region Memory Allocation Benchmarks

        [Test]
        public void Benchmark_MemoryAllocation_10k_SingletonResolution()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
            var container = builder.Build();

            container.Resolve<ISimpleService>();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memoryBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < ITERATIONS_10K; i++)
            {
                var service = container.Resolve<ISimpleService>();
            }

            long memoryAfter = GC.GetTotalMemory(false);
            long allocated = memoryAfter - memoryBefore;

            UnityEngine.Debug.Log($"[MEMORY] Singleton Resolution (10k): {allocated} bytes ({allocated / 1024.0:F2}KB)");

            Assert.LessOrEqual(allocated, 0,
                $"Cached singleton resolution should allocate 0 bytes, got {allocated} bytes");
        }

        [Test]
        public void Benchmark_MemoryAllocation_ContainerBuild()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memoryBefore = GC.GetTotalMemory(true);

            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
            builder.Register<ISingleDependency, SingleDependency>(Lifetime.Singleton);
            builder.Register<IComplexService2, ComplexService2>(Lifetime.Singleton);
            builder.Register<IComplexService, ComplexService>(Lifetime.Singleton);
            var container = builder.Build();

            long memoryAfter = GC.GetTotalMemory(false);
            long allocated = memoryAfter - memoryBefore;

            UnityEngine.Debug.Log($"[MEMORY] Container Build: {allocated} bytes ({allocated / 1024.0:F2}KB)");

            Assert.Less(allocated / 1024.0, 10.0,
                $"Container build should allocate <10KB, got {allocated / 1024.0:F2}KB");
        }

        #endregion

        #region Mixed Workload Benchmarks

        [Test]
        public void Benchmark_MixedWorkload_RealWorld()
        {
            var builder = new ContainerBuilder();
            builder.Register<ISimpleService, SimpleService>(Lifetime.Singleton);
            builder.Register<ISingleDependency, SingleDependency>(Lifetime.Transient);
            builder.Register<IComplexService2, ComplexService2>(Lifetime.Singleton);
            builder.Register<IComplexService, ComplexService>(Lifetime.Scoped);
            var container = builder.Build();

            var result = Benchmark(
                "Mixed Workload - Real World Scenario (1k iterations)",
                () =>
                {
                    for (int i = 0; i < ITERATIONS_1K; i++)
                    {
                        var simple = container.Resolve<ISimpleService>();
                        var transient = container.Resolve<ISingleDependency>();

                        using var scope = container.CreateScope();
                        var scoped = scope.Resolve<IComplexService>();
                        var simple2 = container.Resolve<ISimpleService>();
                    }
                },
                ITERATIONS_1K
            );

            Assert.Less(result.TotalMilliseconds, 30.0,
                $"Mixed workload (1k) should be <30ms, got {result.TotalMilliseconds:F2}ms");
        }

        #endregion

        #region Benchmark Result Helper

        private class BenchmarkResult
        {
            public string Name { get; set; }
            public int Iterations { get; set; }
            public double TotalMilliseconds { get; set; }
            public long MemoryAllocatedBytes { get; set; }

            public double AverageMilliseconds => TotalMilliseconds / Iterations;
            public double MemoryAllocatedKB => MemoryAllocatedBytes / 1024.0;
            public double MemoryPerOperation => (double)MemoryAllocatedBytes / Iterations;
        }

        #endregion
    }
}
