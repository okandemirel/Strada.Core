using System;
using System.Collections.Generic;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    [TestFixture]
    public class ContainerDisposalTests
    {
        private class Tracker
        {
            public List<string> DisposedOrder { get; } = new List<string>();
        }

        private class ServiceA : IDisposable
        {
            private readonly Tracker _tracker;
            public ServiceA(Tracker tracker) => _tracker = tracker;
            public void Dispose() => _tracker.DisposedOrder.Add("ServiceA");
        }

        private class ServiceB : IDisposable
        {
            private readonly Tracker _tracker;
            private readonly ServiceA _serviceA; // Depends on A
            public ServiceB(Tracker tracker, ServiceA serviceA) 
            {
                _tracker = tracker;
                _serviceA = serviceA;
            }
            public void Dispose() => _tracker.DisposedOrder.Add("ServiceB");
        }

        [Test]
        public void Dispose_RespectsDependencyOrder()
        {
            var builder = new ContainerBuilder();
            var tracker = new Tracker();
            
            builder.RegisterInstance(tracker);
            builder.Register<ServiceA>(Lifetime.Singleton);
            builder.Register<ServiceB>(Lifetime.Singleton);
            
            var container = builder.Build();
            var b = container.Resolve<ServiceB>(); 
            
            container.Dispose();
            
            Assert.AreEqual("ServiceB", tracker.DisposedOrder[0], "Dependents should be disposed before dependencies");
            Assert.AreEqual("ServiceA", tracker.DisposedOrder[1]);
        }
    }
}
