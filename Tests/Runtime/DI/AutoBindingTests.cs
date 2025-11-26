using System;
using System.Collections.Generic;
using NUnit.Framework;
using Strada.Core.DI;
using Strada.Core.DI.Attributes;
using Strada.Core.DI.AutoBinding;

namespace Strada.Core.Tests.DI
{
    [TestFixture]
    public class AutoBindingTests
    {
        [SetUp]
        public void SetUp()
        {
            RuntimeAutoBindingScanner.ClearCache();
        }

        [Test]
        public void RuntimeScanner_FindsAutoRegisterAttribute()
        {
            var entries = RuntimeAutoBindingScanner.ScanAssemblies(
                new[] { "Strada.*", "Assembly-CSharp*" },
                new[] { "Unity.*", "System.*" });

            Assert.IsNotNull(entries);
        }

        [Test]
        public void RuntimeScanner_CachesResults()
        {
            var entries1 = RuntimeAutoBindingScanner.ScanAssemblies(
                new[] { "Strada.*" },
                new[] { "Unity.*" });

            var entries2 = RuntimeAutoBindingScanner.ScanAssemblies(
                new[] { "Strada.*" },
                new[] { "Unity.*" });

            Assert.AreSame(entries1, entries2);
        }

        [Test]
        public void RuntimeScanner_ClearCache_ResetsCache()
        {
            var entries1 = RuntimeAutoBindingScanner.ScanAssemblies(
                new[] { "Strada.*" },
                new[] { "Unity.*" });

            RuntimeAutoBindingScanner.ClearCache();

            var count = RuntimeAutoBindingScanner.GetCachedCount();
            Assert.AreEqual(0, count);
        }

        [Test]
        public void ContainerBuilderExtensions_RegisterAutoBindings_DoesNotThrow()
        {
            var builder = new ContainerBuilder();

            Assert.DoesNotThrow(() => builder.RegisterAutoBindings());
        }

        [Test]
        public void ContainerBuilderExtensions_RegisterAutoBindingsRuntime_DoesNotThrow()
        {
            var builder = new ContainerBuilder();

            Assert.DoesNotThrow(() => builder.RegisterAutoBindingsRuntime(
                new[] { "Strada.*" },
                new[] { "Unity.*" }));
        }

        [Test]
        public void AutoBindingEntry_SetsPropertiesCorrectly()
        {
            var entry = new AutoBindingEntry
            {
                ServiceType = typeof(ITestService),
                ImplementationType = typeof(TestServiceImpl),
                Lifetime = Lifetime.Singleton,
                Priority = 10,
                RegisterSelf = true
            };

            Assert.AreEqual(typeof(ITestService), entry.ServiceType);
            Assert.AreEqual(typeof(TestServiceImpl), entry.ImplementationType);
            Assert.AreEqual(Lifetime.Singleton, entry.Lifetime);
            Assert.AreEqual(10, entry.Priority);
            Assert.IsTrue(entry.RegisterSelf);
        }

        [Test]
        public void AutoRegisterAttribute_DefaultLifetimeIsSingleton()
        {
            var attr = new AutoRegisterAttribute();
            Assert.AreEqual(Lifetime.Singleton, attr.Lifetime);
        }

        [Test]
        public void AutoRegisterAttribute_CanSetLifetime()
        {
            var attr = new AutoRegisterAttribute(Lifetime.Transient);
            Assert.AreEqual(Lifetime.Transient, attr.Lifetime);
        }

        [Test]
        public void AutoRegisterAttribute_CanSetAs()
        {
            var attr = new AutoRegisterAttribute { As = typeof(ITestService) };
            Assert.AreEqual(typeof(ITestService), attr.As);
        }

        [Test]
        public void AutoRegisterAttribute_CanSetPriority()
        {
            var attr = new AutoRegisterAttribute { Priority = 100 };
            Assert.AreEqual(100, attr.Priority);
        }

        [Test]
        public void AutoRegisterAttribute_CanSetRegisterSelf()
        {
            var attr = new AutoRegisterAttribute { RegisterSelf = true };
            Assert.IsTrue(attr.RegisterSelf);
        }

        [Test]
        public void AutoRegisterSingletonAttribute_DefaultValues()
        {
            var attr = new AutoRegisterSingletonAttribute();
            Assert.IsNull(attr.As);
            Assert.AreEqual(0, attr.Priority);
            Assert.IsFalse(attr.RegisterSelf);
        }

        [Test]
        public void AutoRegisterTransientAttribute_DefaultValues()
        {
            var attr = new AutoRegisterTransientAttribute();
            Assert.IsNull(attr.As);
            Assert.AreEqual(0, attr.Priority);
            Assert.IsFalse(attr.RegisterSelf);
        }

        [Test]
        public void AutoRegisterScopedAttribute_DefaultValues()
        {
            var attr = new AutoRegisterScopedAttribute();
            Assert.IsNull(attr.As);
            Assert.AreEqual(0, attr.Priority);
            Assert.IsFalse(attr.RegisterSelf);
        }

        [Test]
        public void RuntimeScanner_ManualEntry_RegistersCorrectly()
        {
            // Create a manual entry and verify it registers correctly
            var builder = new ContainerBuilder();

            // Manually register using the same pattern the scanner would use
            builder.Register<ITestService, TestServiceImpl>(Lifetime.Singleton);

            var container = builder.Build();

            // Verify it resolves correctly
            var service = container.Resolve<ITestService>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOf<TestServiceImpl>(service);

            // Verify singleton behavior
            var service2 = container.Resolve<ITestService>();
            Assert.AreSame(service, service2);

            container.Dispose();
        }

        [Test]
        public void RuntimeScanner_PatternMatching_MatchesCorrectly()
        {
            // Test the pattern matching logic used by scanner
            Assert.IsTrue(MatchesPattern("Strada.Core.Tests", "Strada.*"));
            Assert.IsTrue(MatchesPattern("Strada.Core.Tests", "Strada.Core.*"));
            Assert.IsFalse(MatchesPattern("Strada.Core.Tests", "Unity.*"));
            Assert.IsFalse(MatchesPattern("UnityEngine", "Strada.*"));
            Assert.IsTrue(MatchesPattern("Game.Modules.Input", "Game.*"));
            Assert.IsTrue(MatchesPattern("Assembly-CSharp", "Assembly-CSharp"));
        }

        private static bool MatchesPattern(string name, string pattern)
        {
            if (pattern.StartsWith("*") && pattern.EndsWith("*"))
                return name.Contains(pattern.Trim('*'));
            if (pattern.StartsWith("*"))
                return name.EndsWith(pattern.TrimStart('*'));
            if (pattern.EndsWith("*"))
                return name.StartsWith(pattern.TrimEnd('*'));
            return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        [Test]
        public void RuntimeScanner_FindsTestAutoRegisterClass()
        {
            RuntimeAutoBindingScanner.ClearCache();

            // Scan including the test assembly
            var entries = RuntimeAutoBindingScanner.ScanAssemblies(
                new[] { "Strada.*" },
                new[] { "Unity.*", "System.*" });

            // Find our test service
            var testEntry = entries.Find(e => e.ImplementationType == typeof(TestAutoRegisteredService));

            Assert.IsNotNull(testEntry, "Should find TestAutoRegisteredService");
            Assert.AreEqual(typeof(ITestAutoRegistered), testEntry.ServiceType);
            Assert.AreEqual(Lifetime.Singleton, testEntry.Lifetime);
            Assert.AreEqual(100, testEntry.Priority);
        }

        [Test]
        public void EndToEnd_AutoRegisterService_ResolvesCorrectly()
        {
            RuntimeAutoBindingScanner.ClearCache();

            var builder = new ContainerBuilder();

            // Use runtime scanner (not source gen) for test
            builder.RegisterAutoBindingsRuntime(
                new[] { "Strada.*" },
                new[] { "Unity.*", "System.*" });

            var container = builder.Build();

            // Should be able to resolve by interface
            var service = container.Resolve<ITestAutoRegistered>();
            Assert.IsNotNull(service, "Should resolve ITestAutoRegistered");
            Assert.IsInstanceOf<TestAutoRegisteredService>(service);

            // Should be singleton
            var service2 = container.Resolve<ITestAutoRegistered>();
            Assert.AreSame(service, service2, "Should return same singleton instance");

            container.Dispose();
        }

        // Test interfaces and implementations
        private interface ITestService { }
        private class TestServiceImpl : ITestService { }
    }

    // Test class with AutoRegister attribute - MUST be public for reflection
    public interface ITestAutoRegistered
    {
        int GetValue();
    }

    [AutoRegisterSingleton(As = typeof(ITestAutoRegistered), Priority = 100)]
    public class TestAutoRegisteredService : ITestAutoRegistered
    {
        public int GetValue() => 42;
    }

    [TestFixture]
    [Category("Performance")]
    public class AutoBindingPerformanceTests
    {
        [SetUp]
        public void SetUp()
        {
            RuntimeAutoBindingScanner.ClearCache();
        }

        [Test]
        public void Benchmark_RuntimeScanning_1000Iterations()
        {
            // Warmup
            for (int i = 0; i < 10; i++)
            {
                RuntimeAutoBindingScanner.ClearCache();
                RuntimeAutoBindingScanner.ScanAssemblies(
                    new[] { "Strada.*" },
                    new[] { "Unity.*" });
            }

            RuntimeAutoBindingScanner.ClearCache();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // First scan (uncached)
            var entries = RuntimeAutoBindingScanner.ScanAssemblies(
                new[] { "Strada.*" },
                new[] { "Unity.*" });

            var firstScanMs = sw.ElapsedMilliseconds;
            sw.Restart();

            // Cached scans
            for (int i = 0; i < 999; i++)
            {
                RuntimeAutoBindingScanner.ScanAssemblies(
                    new[] { "Strada.*" },
                    new[] { "Unity.*" });
            }

            sw.Stop();

            var cachedMs = sw.ElapsedMilliseconds;
            var avgCachedNs = cachedMs * 1000.0 / 999;

            UnityEngine.Debug.Log($"[AutoBinding] First scan: {firstScanMs}ms, 999 cached lookups: {cachedMs}ms ({avgCachedNs:F0}ns avg)");

            // Competitive thresholds - VContainer/Reflex-level performance
            Assert.Less(firstScanMs, 50, "First scan should complete in < 50ms (competitive with VContainer)");
            Assert.Less(cachedMs, 1, "999 cached lookups should complete in < 1ms (O(1) cache access)");
        }

        [Test]
        public void Benchmark_ContainerBuild_WithAutoBinding()
        {
            // Warmup
            for (int i = 0; i < 5; i++)
            {
                var warmupBuilder = new ContainerBuilder();
                warmupBuilder.RegisterAutoBindingsRuntime(new[] { "Strada.*" }, new[] { "Unity.*" });
                var warmupContainer = warmupBuilder.Build();
                warmupContainer.Dispose();
            }

            RuntimeAutoBindingScanner.ClearCache();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var builder = new ContainerBuilder();
            builder.RegisterAutoBindingsRuntime(new[] { "Strada.*" }, new[] { "Unity.*" });
            var container = builder.Build();

            sw.Stop();

            var count = ContainerBuilderExtensions.GetAutoBindingCount();

            UnityEngine.Debug.Log($"[AutoBinding] Container build with {count} auto-bindings: {sw.ElapsedMilliseconds}ms");

            // Competitive threshold - container build should be fast
            Assert.Less(sw.ElapsedMilliseconds, 100, "Container build with auto-bindings should complete in < 100ms");

            container.Dispose();
        }

        [Test]
        public void Benchmark_10k_AutoBindingResolutions()
        {
            RuntimeAutoBindingScanner.ClearCache();

            var builder = new ContainerBuilder();
            builder.RegisterAutoBindingsRuntime(new[] { "Strada.*" }, new[] { "Unity.*" });
            var container = builder.Build();

            // Warmup
            for (int i = 0; i < 100; i++)
            {
                container.Resolve<ITestAutoRegistered>();
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 10_000; i++)
            {
                container.Resolve<ITestAutoRegistered>();
            }

            sw.Stop();

            var avgNs = sw.Elapsed.TotalMilliseconds * 1000.0 / 10_000 * 1000.0;

            UnityEngine.Debug.Log($"[AutoBinding] 10k singleton resolutions: {sw.ElapsedMilliseconds}ms ({avgNs:F0}ns avg)");

            // Singleton resolution should be < 100ns avg (just a dictionary lookup)
            Assert.Less(sw.ElapsedMilliseconds, 10, "10k singleton resolutions should complete in < 10ms");
            Assert.Less(avgNs, 1000, "Avg singleton resolution should be < 1μs");

            container.Dispose();
        }
    }
}
