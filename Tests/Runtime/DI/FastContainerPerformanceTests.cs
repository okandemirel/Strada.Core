using NUnit.Framework;
using Unity.PerformanceTesting;
using Strada.Core.DI;

namespace Strada.Core.Tests.Performance
{
    [TestFixture]
    public class FastContainerPerformanceTests
    {
        public interface IServiceA { }
        public interface IServiceB { }
        public interface IServiceC { }
        public interface IServiceD { }

        public class ServiceA : IServiceA { }
        public class ServiceB : IServiceB
        {
            public ServiceB(IServiceA a) { }
        }
        public class ServiceC : IServiceC
        {
            public ServiceC(IServiceA a, IServiceB b) { }
        }
        public class ServiceD : IServiceD
        {
            public ServiceD(IServiceA a, IServiceB b, IServiceC c) { }
        }

        [Test, Performance]
        public void Benchmark_SingleResolution_Transient_1000()
        {
            var builder = new ContainerBuilder();
            builder.UseFastContainer();
            builder.Register<IServiceA, ServiceA>(Lifetime.Transient);
            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var service = container.Resolve<IServiceA>();
                }
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(5)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void Benchmark_SingleResolution_Singleton_1000()
        {
            var builder = new ContainerBuilder();
            builder.UseFastContainer();
            builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var service = container.Resolve<IServiceA>();
                }
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(5)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void Benchmark_ComplexDependencyGraph_10000()
        {
            var builder = new ContainerBuilder();
            builder.UseFastContainer();
            builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
            builder.Register<IServiceB, ServiceB>(Lifetime.Singleton);
            builder.Register<IServiceC, ServiceC>(Lifetime.Singleton);
            builder.Register<IServiceD, ServiceD>(Lifetime.Transient);
            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var service = container.Resolve<IServiceD>();
                }
            })
            .WarmupCount(10)
            .MeasurementCount(50)
            .IterationsPerMeasurement(3)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void Benchmark_MemoryAllocation_Transient_1000()
        {
            var builder = new ContainerBuilder();
            builder.UseFastContainer();
            builder.Register<IServiceA, ServiceA>(Lifetime.Transient);
            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var service = container.Resolve<IServiceA>();
                }
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .IterationsPerMeasurement(1)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void Benchmark_MemoryAllocation_Singleton_1000()
        {
            var builder = new ContainerBuilder();
            builder.UseFastContainer();
            builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var service = container.Resolve<IServiceA>();
                }
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .IterationsPerMeasurement(1)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void Benchmark_ContainerBuild_100Registrations()
        {
            Measure.Method(() =>
            {
                var builder = new ContainerBuilder();
                builder.UseFastContainer();

                for (int i = 0; i < 100; i++)
                {
                    builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
                }

                var container = builder.Build();
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .IterationsPerMeasurement(1)
            .GC()
            .Run();
        }

        [Test, Performance]
        public void Benchmark_ScopeCreation_1000()
        {
            var builder = new ContainerBuilder();
            builder.UseFastContainer();
            builder.Register<IServiceA, ServiceA>(Lifetime.Scoped);
            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    using var scope = container.CreateScope();
                    var service = scope.Resolve<IServiceA>();
                }
            })
            .WarmupCount(10)
            .MeasurementCount(50)
            .IterationsPerMeasurement(3)
            .GC()
            .Run();
        }
    }
}
