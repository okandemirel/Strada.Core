using System;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    [TestFixture]
    public class OpenGenericTests
    {
        private interface IService<T> { }
        private class Service<T> : IService<T> { }

        [Test]
        public void Resolve_OpenGeneric_Success()
        {
            var builder = new ContainerBuilder();
            
            builder.Register<IService<int>, Service<int>>();
            var container = builder.Build();
            
            var instance = container.Resolve<IService<int>>();
            Assert.IsNotNull(instance);
        }
    }
}
