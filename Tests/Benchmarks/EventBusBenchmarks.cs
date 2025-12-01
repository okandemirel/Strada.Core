using System;
using NUnit.Framework;
using Strada.Core.Communication;
using Unity.PerformanceTesting;

namespace Strada.Core.Tests.Benchmarks
{
    public class EventBusBenchmarks
    {
        private EventBus _bus;

        [SetUp]
        public void Setup()
        {
            _bus = new EventBus();
        }

        [TearDown]
        public void Teardown()
        {
            _bus?.Dispose();
        }

        [Test, Performance]
        public void Publish_Benchmark()
        {
            _bus.Subscribe<TestEvent>(e => { });

            Measure.Method(() =>
            {
                _bus.Publish(new TestEvent { Value = 1 });
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1000)
            .Run();
        }

        [Test, Performance]
        public void Publish_NoSubscribers_Benchmark()
        {
            Measure.Method(() =>
            {
                _bus.Publish(new TestEvent { Value = 1 });
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1000)
            .Run();
        }

        private struct TestEvent
        {
            public int Value;
        }
    }
}
