using System;
using NUnit.Framework;
using Strada.Core.DI;
using System.Diagnostics;

namespace Strada.Core.Tests.Performance
{
    public class Level4
    {
        public int Value = 4;
    }

    public class Level3
    {
        public Level4 Dependency;
        public Level3(Level4 dep) { Dependency = dep; }
    }

    public class Level2
    {
        public Level3 Dependency;
        public Level2(Level3 dep) { Dependency = dep; }
    }

    public class Level1
    {
        public Level2 Dependency;
        public Level1(Level2 dep) { Dependency = dep; }
    }

    [TestFixture]
    public class DIPerformanceTests
    {
        private IContainer _container;

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

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var instance = _container.Resolve<Level1>();
                Assert.NotNull(instance);
                Assert.NotNull(instance.Dependency);
                Assert.NotNull(instance.Dependency.Dependency);
                Assert.NotNull(instance.Dependency.Dependency.Dependency);
            }
            sw.Stop();

            long memoryBefore = GC.GetTotalMemory(true);
            for (int i = 0; i < iterations; i++)
            {
                var instance = _container.Resolve<Level1>();
            }
            long memoryAfter = GC.GetTotalMemory(true);
            long gcAllocation = (memoryAfter - memoryBefore) / 1024;

            UnityEngine.Debug.Log($"[STRADA BENCHMARK] 10k Transient Resolutions:");
            UnityEngine.Debug.Log($"  Time: {sw.ElapsedMilliseconds}ms (Target: <12ms, Reflex: 15ms)");
            UnityEngine.Debug.Log($"  GC: {gcAllocation}KB (Target: <60KB, Reflex: 70KB)");
            UnityEngine.Debug.Log($"  Avg: {sw.Elapsed.TotalMilliseconds / iterations:F4}ms per resolution");

            Assert.Less(sw.ElapsedMilliseconds, 12, "Failed to beat Reflex performance target");
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

            Assert.Less(sw.ElapsedMilliseconds, 10, "Singleton resolution too slow");
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
    }
}
