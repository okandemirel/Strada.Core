using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.Sync;

namespace Strada.Core.Tests.Benchmarks
{
    [TestFixture]
    [Category("Benchmark")]
    public class ComputedPropertyBenchmarks
    {
        [Test]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(20)]
        public void ComputedProperty_FromMany_Dependencies(int dependencyCount)
        {
            const int iterations = 1000;

            // Create dependencies
            var dependencies = new ReactiveProperty<int>[dependencyCount];
            for (int i = 0; i < dependencyCount; i++)
            {
                dependencies[i] = new ReactiveProperty<int>(i);
            }

            // Create computed property using FromMany
            var computed = ComputedProperty<int>.FromMany(
                () =>
                {
                    int sum = 0;
                    for (int i = 0; i < dependencyCount; i++)
                    {
                        sum += dependencies[i].Value;
                    }
                    return sum;
                },
                dependencies);

            int subscribeCallCount = 0;
            computed.Subscribe(_ => subscribeCallCount++);

            // Warmup
            for (int i = 0; i < 100; i++)
            {
                dependencies[0].Value = i;
            }
            subscribeCallCount = 0;

            // Benchmark dependency changes
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                dependencies[i % dependencyCount].Value = i;
            }
            sw.Stop();

            double avgNs = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[ComputedProperty] FromMany with {dependencyCount} dependencies ({iterations} updates):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNs:F0}ns per update");
            UnityEngine.Debug.Log($"  Notifications triggered: {subscribeCallCount}");

            // Cleanup
            computed.Dispose();
            foreach (var dep in dependencies)
            {
                dep.Dispose();
            }
        }

        [Test]
        public void ComputedProperty_ValueAccess_Performance()
        {
            const int iterations = 10000;

            var dep1 = new ReactiveProperty<int>(1);
            var dep2 = new ReactiveProperty<int>(2);
            var computed = ComputedProperty<int>.From(dep1, dep2, (a, b) => a + b);

            // Warmup
            for (int i = 0; i < 100; i++)
            {
                var _ = computed.Value;
            }

            // Benchmark value access (should be cached)
            var sw = Stopwatch.StartNew();
            int sum = 0;
            for (int i = 0; i < iterations; i++)
            {
                sum += computed.Value;
            }
            sw.Stop();

            double avgNs = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[ComputedProperty] Value Access (cached) ({iterations} reads):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNs:F1}ns per read");

            computed.Dispose();
            dep1.Dispose();
            dep2.Dispose();
        }

        [Test]
        public void ComputedProperty_ChainedDependencies_Performance()
        {
            const int chainLength = 10;
            const int iterations = 1000;

            var source = new ReactiveProperty<int>(0);
            var computedChain = new ComputedProperty<int>[chainLength];

            // Create chain: source -> computed[0] -> computed[1] -> ... -> computed[n-1]
            computedChain[0] = ComputedProperty<int>.From(source, v => v + 1);
            for (int i = 1; i < chainLength; i++)
            {
                int index = i;
                computedChain[i] = ComputedProperty<int>.From(computedChain[i - 1], v => v + 1);
            }

            int finalValue = 0;
            computedChain[chainLength - 1].Subscribe(v => finalValue = v);

            // Warmup
            for (int i = 0; i < 100; i++)
            {
                source.Value = i;
            }

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                source.Value = i;
            }
            sw.Stop();

            double avgNs = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[ComputedProperty] Chain ({chainLength} deep, {iterations} updates):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNs:F0}ns per update");
            UnityEngine.Debug.Log($"  Final computed value: {finalValue}");

            // Cleanup
            foreach (var c in computedChain)
                c.Dispose();
            source.Dispose();
        }
    }
}
