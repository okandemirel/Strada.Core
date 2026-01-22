using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.DI
{
    [TestFixture]
    [Category("ThreadSafety")]
    public class ContainerThreadSafetyTests
    {
        [Test]
        public void Resolve_Singleton_ConcurrentAccess_ReturnsSameInstance()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            using var container = builder.Build();

            const int threadCount = 4;
            const int iterationsPerThread = 25;
            var instances = new ITestService[threadCount * iterationsPerThread];
            var tasks = new Task[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                int threadIndex = t;
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        int index = threadIndex * iterationsPerThread + i;
                        instances[index] = container.Resolve<ITestService>();
                    }
                });
            }

            Task.WaitAll(tasks);

            // All instances should be the same singleton
            var firstInstance = instances[0];
            Assert.IsNotNull(firstInstance);

            foreach (var instance in instances)
            {
                Assert.AreSame(firstInstance, instance);
            }
        }

        [Test]
        public void Resolve_Transient_ConcurrentAccess_CreatesDifferentInstances()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Transient);
            using var container = builder.Build();

            const int threadCount = 4;
            const int iterationsPerThread = 10;
            var instances = new ITestService[threadCount * iterationsPerThread];
            var tasks = new Task[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                int threadIndex = t;
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        int index = threadIndex * iterationsPerThread + i;
                        instances[index] = container.Resolve<ITestService>();
                    }
                });
            }

            Task.WaitAll(tasks);

            // Verify all instances are not null
            foreach (var instance in instances)
            {
                Assert.IsNotNull(instance);
            }

            // Spot check: first and last should be different
            Assert.AreNotSame(instances[0], instances[instances.Length - 1]);
        }

        [Test]
        public void TryResolve_ConcurrentAccess_DoesNotThrow()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            using var container = builder.Build();

            const int threadCount = 4;
            const int iterationsPerThread = 25;
            var tasks = new Task[threadCount];
            var successCount = 0;

            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        if (container.TryResolve<ITestService>(out _))
                        {
                            Interlocked.Increment(ref successCount);
                        }
                    }
                });
            }

            Task.WaitAll(tasks);

            Assert.AreEqual(threadCount * iterationsPerThread, successCount);
        }

        [Test]
        public void IsRegistered_ConcurrentAccess_DoesNotThrow()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            using var container = builder.Build();

            const int threadCount = 4;
            const int iterationsPerThread = 25;
            var tasks = new Task[threadCount];
            var trueCount = 0;

            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        if (container.IsRegistered<ITestService>())
                        {
                            Interlocked.Increment(ref trueCount);
                        }
                    }
                });
            }

            Task.WaitAll(tasks);
            Assert.AreEqual(threadCount * iterationsPerThread, trueCount);
        }

        private interface ITestService { }
        private class TestService : ITestService { }
    }
}
