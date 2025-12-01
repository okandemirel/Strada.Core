using System;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    [TestFixture]
    public class CircularDependencyTests
    {
        private interface IA { }
        private interface IB { }

        private class ServiceA : IA
        {
            public ServiceA(IB b) { }
        }

        private class ServiceB : IB
        {
            public ServiceB(IA a) { }
        }

        [Test]
        public void Resolve_CircularDependency_ThrowsException()
        {
            var builder = new ContainerBuilder();
            builder.Register<IA, ServiceA>();
            builder.Register<IB, ServiceB>();
            
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }
    }
}
