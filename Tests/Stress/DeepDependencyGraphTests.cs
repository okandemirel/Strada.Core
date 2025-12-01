using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Strada.Core.DI;
using UnityEngine;

namespace Strada.Core.Tests.Stress
{
    public class DeepDependencyGraphTests
    {
        private Container _container;

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
        }

        [Test]
        public void Parallel_Singleton_Resolution_ThreadSafety()
        {
            var builder = new ContainerBuilder();
            
            builder.RegisterFactory<SharedSingleton>(c => 
            {
                Thread.Sleep(5);
                return new SharedSingleton();
            }, Lifetime.Singleton);

            _container = builder.Build() as Container;

            var instances = new SharedSingleton[100];
            
            StressTestRunner.Run("Parallel Singleton Resolve", () =>
            {
                Parallel.For(0, 100, i =>
                {
                    instances[i] = _container.Resolve<SharedSingleton>();
                });
            });

            var first = instances[0];
            Assert.IsNotNull(first);
            for (int i = 1; i < 100; i++)
            {
                Assert.AreSame(first, instances[i], "All resolved instances should be the same object (Singleton)");
            }
        }

        private class SharedSingleton { }
    }
}
