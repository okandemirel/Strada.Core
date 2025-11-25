using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Reactive;
using Unity.PerformanceTesting;

namespace Strada.Core.Tests.Performance
{
    [TestFixture]
    [Category("Performance")]
    public sealed class ReactiveSystemPerformanceTests
    {
        private struct TestComponent : IComponent
        {
            public int Value;
        }

        [Test, Performance]
        public void Benchmark_ReactiveAdd_10k()
        {
            var storage = new ReactiveComponentStorage<TestComponent>(16384, 16384);
            var addCount = 0;

            storage.SubscribeOnAdd((entity, component) => addCount++);

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    storage.Add(i, new TestComponent { Value = i });
                }
            })
            .WarmupCount(1)
            .MeasurementCount(5)
            .SetUp(() =>
            {
                storage.Clear();
                addCount = 0;
            })
            .Run();

            storage.Dispose();
        }

        [Test, Performance]
        public void Benchmark_ReactiveChange_10k()
        {
            var storage = new ReactiveComponentStorage<TestComponent>(16384, 16384);
            var changeCount = 0;

            for (var i = 0; i < 10000; i++)
            {
                storage.Add(i, new TestComponent { Value = i });
            }

            storage.SubscribeOnChange((entity, old, newVal) => changeCount++);

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    storage.Set(i, new TestComponent { Value = i + 1 });
                }
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();

            storage.Dispose();
        }

        [Test, Performance]
        public void Benchmark_MultipleSubscribers_10k()
        {
            var storage = new ReactiveComponentStorage<TestComponent>(16384, 16384);
            var counts = new int[5];

            storage.SubscribeOnAdd((e, c) => counts[0]++);
            storage.SubscribeOnAdd((e, c) => counts[1]++);
            storage.SubscribeOnAdd((e, c) => counts[2]++);
            storage.SubscribeOnAdd((e, c) => counts[3]++);
            storage.SubscribeOnAdd((e, c) => counts[4]++);

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    storage.Add(i, new TestComponent { Value = i });
                }
            })
            .WarmupCount(1)
            .MeasurementCount(5)
            .SetUp(() =>
            {
                storage.Clear();
                for (var i = 0; i < 5; i++) counts[i] = 0;
            })
            .Run();

            storage.Dispose();
        }

        [Test, Performance]
        public void Benchmark_NonReactive_Baseline_10k()
        {
            var storage = new Strada.Core.ECS.Storage.ComponentStorage<TestComponent>(16384, 16384);

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    storage.Add(i, new TestComponent { Value = i });
                }
            })
            .WarmupCount(1)
            .MeasurementCount(5)
            .SetUp(() => storage.Clear())
            .Run();

            storage.Dispose();
        }
    }
}
