using System;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    /// <summary>
    /// Comprehensive tests for the ContainerScope class.
    /// </summary>
    [TestFixture]
    public class ContainerScopeTests
    {

        public interface ITestService { }
        public interface ITestRepository { }
        public interface IScopedService { }

        public class TestService : ITestService { }
        public class TestRepository : ITestRepository { }
        public class ScopedService : IScopedService { }

        public class DisposableService : IScopedService, IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        public class ServiceWithDependency : IScopedService
        {
            public ITestService Service { get; }

            public ServiceWithDependency(ITestService service)
            {
                Service = service;
            }
        }

        [Test]
        public void CreateScope_ReturnsValidScope()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();

            var scope = container.CreateScope();

            Assert.IsNotNull(scope);
            Assert.AreSame(container, scope.Parent);
        }

        [Test]
        public void CreateScope_MultipleTimes_ReturnsDifferentScopes()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();

            var scope1 = container.CreateScope();
            var scope2 = container.CreateScope();

            Assert.AreNotSame(scope1, scope2);
            Assert.AreSame(container, scope1.Parent);
            Assert.AreSame(container, scope2.Parent);
        }

        [Test]
        public void CreateScope_FromScope_CreatesChildScope()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var parentScope = container.CreateScope();

            var childScope = parentScope.CreateScope();

            Assert.IsNotNull(childScope);
            Assert.AreSame(container, childScope.Parent); // Parent is still root container
        }


        [Test]
        public void Resolve_ScopedService_ReturnsSameInstanceWithinScope()
        {
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            var instance1 = scope.Resolve<IScopedService>();
            var instance2 = scope.Resolve<IScopedService>();

            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void Resolve_ScopedService_ReturnsDifferentInstancesBetweenScopes()
        {
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope1 = container.CreateScope();
            var scope2 = container.CreateScope();

            var instance1 = scope1.Resolve<IScopedService>();
            var instance2 = scope2.Resolve<IScopedService>();

            Assert.AreNotSame(instance1, instance2);
        }

        [Test]
        public void Resolve_SingletonFromScope_ReturnsSameInstanceAsParent()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            var container = builder.Build();
            var scope = container.CreateScope();

            var containerInstance = container.Resolve<ITestService>();
            var scopeInstance = scope.Resolve<ITestService>();

            Assert.AreSame(containerInstance, scopeInstance);
        }

        [Test]
        public void Resolve_TransientFromScope_ReturnsDifferentInstances()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Transient);
            var container = builder.Build();
            var scope = container.CreateScope();

            var instance1 = scope.Resolve<ITestService>();
            var instance2 = scope.Resolve<ITestService>();

            Assert.AreNotSame(instance1, instance2);
        }


        [Test]
        public void Resolve_ScopedWithDependencies_InjectsDependencies()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            builder.Register<IScopedService, ServiceWithDependency>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            var instance = scope.Resolve<IScopedService>();

            var serviceDep = (ServiceWithDependency)instance;
            Assert.IsNotNull(serviceDep.Service);
        }


        [Test]
        public void Resolve_UnregisteredType_ThrowsException()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var scope = container.CreateScope();

            var ex = Assert.Throws<InvalidOperationException>(() => scope.Resolve<ITestService>());
            Assert.That(ex.Message, Does.Contain("not registered"));
        }

        [Test]
        public void TryResolve_FromScope_Works()
        {
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            var success = scope.TryResolve<IScopedService>(out var instance);

            Assert.IsTrue(success);
            Assert.IsNotNull(instance);
        }

        [Test]
        public void IsRegistered_FromScope_Works()
        {
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            var isRegistered = scope.IsRegistered<IScopedService>();

            Assert.IsTrue(isRegistered);
        }


        [Test]
        public void Dispose_DisposesAllScopedInstances()
        {
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, DisposableService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();
            var instance = (DisposableService)scope.Resolve<IScopedService>();

            scope.Dispose();

            Assert.IsTrue(instance.IsDisposed);
        }

        [Test]
        public void Dispose_DoesNotDisposeParentSingletons()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            var container = builder.Build();
            var scope = container.CreateScope();
            scope.Resolve<ITestService>();

            scope.Dispose();

            var instance = container.Resolve<ITestService>();
            Assert.IsNotNull(instance);
        }

        [Test]
        public void Resolve_AfterDisposal_ThrowsException()
        {
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();
            scope.Dispose();

            Assert.Throws<ObjectDisposedException>(() => scope.Resolve<IScopedService>());
        }

        [Test]
        public void CreateScope_AfterDisposal_ThrowsException()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var scope = container.CreateScope();
            scope.Dispose();

            Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
        }

        [Test]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var scope = container.CreateScope();

            scope.Dispose();
            Assert.DoesNotThrow(() => scope.Dispose());
        }
    }
}
