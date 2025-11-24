using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.DI;
using Unity.PerformanceTesting;

namespace Strada.Core.Tests.Performance
{
    [TestFixture]
    public class DIPerformanceBenchmarks
    {
        private interface IServiceA { }
        private interface IServiceB { IServiceA A { get; } }
        private interface IServiceC { IServiceB B { get; } }

        private class ServiceA : IServiceA { }
        private class ServiceB : IServiceB
        {
            public IServiceA A { get; }
            public ServiceB(IServiceA a) => A = a;
        }
        private class ServiceC : IServiceC
        {
            public IServiceB B { get; }
            public ServiceC(IServiceB b) => B = b;
        }

        [Test, Performance]
        public void Strada_FastContainer_10k_Resolutions_Transient()
        {
            var builder = new ContainerBuilder()
                .UseFastContainer(true);

            builder.Register<IServiceA, ServiceA>(Lifetime.Transient);
            builder.Register<IServiceB, ServiceB>(Lifetime.Transient);
            builder.Register<IServiceC, ServiceC>(Lifetime.Transient);

            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var service = container.Resolve<IServiceC>();
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();
        }

        [Test, Performance]
        public void Strada_FastContainer_10k_Resolutions_Singleton()
        {
            var builder = new ContainerBuilder()
                .UseFastContainer(true);

            builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
            builder.Register<IServiceB, ServiceB>(Lifetime.Singleton);
            builder.Register<IServiceC, ServiceC>(Lifetime.Singleton);

            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var service = container.Resolve<IServiceC>();
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();
        }

        [Test, Performance]
        public void Strada_StandardContainer_10k_Resolutions_Transient()
        {
            var builder = new ContainerBuilder()
                .UseFastContainer(false);

            builder.Register<IServiceA, ServiceA>(Lifetime.Transient);
            builder.Register<IServiceB, ServiceB>(Lifetime.Transient);
            builder.Register<IServiceC, ServiceC>(Lifetime.Transient);

            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var service = container.Resolve<IServiceC>();
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();
        }

        [Test, Performance]
        public void Strada_StandardContainer_10k_Resolutions_Singleton()
        {
            var builder = new ContainerBuilder()
                .UseFastContainer(false);

            builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
            builder.Register<IServiceB, ServiceB>(Lifetime.Singleton);
            builder.Register<IServiceC, ServiceC>(Lifetime.Singleton);

            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var service = container.Resolve<IServiceC>();
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();
        }

        [Test]
        public void Strada_FastContainer_Manual_Timing_10k_Transient()
        {
            var builder = new ContainerBuilder()
                .UseFastContainer(true);

            builder.Register<IServiceA, ServiceA>(Lifetime.Transient);
            builder.Register<IServiceB, ServiceB>(Lifetime.Transient);
            builder.Register<IServiceC, ServiceC>(Lifetime.Transient);

            var container = builder.Build();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                var service = container.Resolve<IServiceC>();
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[Strada FastContainer] 10k transient resolutions: {sw.ElapsedMilliseconds}ms");
            Assert.Less(sw.ElapsedMilliseconds, 9, "Target: <9ms for 10k resolutions");
        }

        [Test]
        public void Strada_FastContainer_Manual_Timing_10k_Singleton()
        {
            var builder = new ContainerBuilder()
                .UseFastContainer(true);

            builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
            builder.Register<IServiceB, ServiceB>(Lifetime.Singleton);
            builder.Register<IServiceC, ServiceC>(Lifetime.Singleton);

            var container = builder.Build();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                var service = container.Resolve<IServiceC>();
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[Strada FastContainer] 10k singleton resolutions: {sw.ElapsedMilliseconds}ms");
            Assert.Less(sw.ElapsedMilliseconds, 1, "Target: <1ms for 10k singleton resolutions");
        }

        [Test]
        public void Strada_FastContainer_Memory_Allocation_Test()
        {
            var builder = new ContainerBuilder()
                .UseFastContainer(true);

            builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
            builder.Register<IServiceB, ServiceB>(Lifetime.Singleton);
            builder.Register<IServiceC, ServiceC>(Lifetime.Singleton);

            var container = builder.Build();

            var initialMemory = System.GC.GetTotalMemory(true);

            for (int i = 0; i < 10000; i++)
            {
                var service = container.Resolve<IServiceC>();
            }

            var finalMemory = System.GC.GetTotalMemory(false);
            var allocated = finalMemory - initialMemory;

            UnityEngine.Debug.Log($"[Strada FastContainer] Memory allocated for 10k singleton resolutions: {allocated / 1024}KB");
            Assert.Less(allocated, 50 * 1024, "Target: <50KB memory allocation");
        }

        [Test, Performance]
        public void Strada_FastContainer_100k_Resolutions_Singleton()
        {
            var builder = new ContainerBuilder()
                .UseFastContainer(true);

            builder.Register<IServiceA, ServiceA>(Lifetime.Singleton);
            builder.Register<IServiceB, ServiceB>(Lifetime.Singleton);
            builder.Register<IServiceC, ServiceC>(Lifetime.Singleton);

            var container = builder.Build();

            Measure.Method(() =>
            {
                for (int i = 0; i < 100000; i++)
                {
                    var service = container.Resolve<IServiceC>();
                }
            })
            .WarmupCount(3)
            .MeasurementCount(5)
            .Run();
        }

        [Test]
        public void Strada_FastContainer_Container_Creation_Performance()
        {
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < 100; i++)
            {
                var builder = new ContainerBuilder()
                    .UseFastContainer(true);

                builder.Register<IServiceA, ServiceA>(Lifetime.Transient);
                builder.Register<IServiceB, ServiceB>(Lifetime.Transient);
                builder.Register<IServiceC, ServiceC>(Lifetime.Transient);

                var container = builder.Build();
            }

            sw.Stop();

            UnityEngine.Debug.Log($"[Strada FastContainer] 100 container creations: {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / 100.0}ms avg)");
            Assert.Less(sw.ElapsedMilliseconds, 200, "Target: <2ms per container creation");
        }
    }
}
