using System;
using NUnit.Framework;
using Strada.Core.DI;
using Unity.PerformanceTesting;

namespace Strada.Core.Tests.Benchmarks
{
    public class ContainerBenchmarks
    {
        private Container _container;

        [SetUp]
        public void Setup()
        {
            var builder = new ContainerBuilder();
            builder.Register<TestService>(Lifetime.Transient);
            builder.Register<SingletonService>(Lifetime.Singleton);
            _container = builder.Build() as Container;
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
        }

        [Test, Performance]
        public void Resolve_Transient_Benchmark()
        {
            Measure.Method(() =>
            {
                _container.Resolve<TestService>();
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1000)
            .Run();
        }

        [Test, Performance]
        public void Resolve_Singleton_Benchmark()
        {
            _container.Resolve<SingletonService>();

            Measure.Method(() =>
            {
                _container.Resolve<SingletonService>();
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1000)
            .Run();
        }

        private class TestService { }
        private class SingletonService { }
    }
}
