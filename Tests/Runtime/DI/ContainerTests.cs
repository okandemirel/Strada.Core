using System;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    /// <summary>
    /// Comprehensive tests for the Container class.
    /// </summary>
    [TestFixture]
    public class ContainerTests
    {

        public interface ITestService { }
        public interface ITestRepository { }
        public interface ITestController { }
        public interface IGenericService<T> { }

        public class TestService : ITestService { }

        public class TestRepository : ITestRepository { }

        public class TestController : ITestController
        {
            public ITestService Service { get; }
            public ITestRepository Repository { get; }

            public TestController(ITestService service, ITestRepository repository)
            {
                Service = service;
                Repository = repository;
            }
        }

        public class DisposableService : ITestService, IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        public class ServiceWithNoDependencies : ITestService
        {
            public ServiceWithNoDependencies() { }
        }

        public class ServiceWithDependency : ITestService
        {
            public ITestRepository Repository { get; }

            public ServiceWithDependency(ITestRepository repository)
            {
                Repository = repository;
            }
        }

        public interface IServiceA { }
        public interface IServiceB { }

        public class ServiceA : IServiceA
        {
            public ServiceA(IServiceB serviceB) { }
        }

        public class ServiceB : IServiceB
        {
            public ServiceB(IServiceA serviceA) { }
        }

        public class ConcreteService
        {
            public string Name { get; set; } = "Test";
        }

        [Test]
        public void Resolve_WithRegisteredType_ReturnsInstance()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            var container = builder.Build();

            var instance = container.Resolve<ITestService>();

            Assert.IsNotNull(instance);
            Assert.IsInstanceOf<TestService>(instance);
        }

        [Test]
        public void Resolve_WithUnregisteredType_ThrowsException()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();

            var ex = Assert.Throws<InvalidOperationException>(() => container.Resolve<ITestService>());
            Assert.That(ex.Message, Does.Contain("not registered"));
        }

        [Test]
        public void Resolve_WithNullType_ThrowsArgumentNullException()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();

            Assert.Throws<ArgumentNullException>(() => container.Resolve(null));
        }

        [Test]
        public void Resolve_ConcreteType_ReturnsInstance()
        {
            var builder = new ContainerBuilder();
            builder.Register<ConcreteService>();
            var container = builder.Build();

            var instance = container.Resolve<ConcreteService>();

            Assert.IsNotNull(instance);
            Assert.AreEqual("Test", instance.Name);
        }

        [Test]
        public void Resolve_Singleton_ReturnsSameInstance()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            var container = builder.Build();

            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void Resolve_Transient_ReturnsDifferentInstances()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Transient);
            var container = builder.Build();

            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            Assert.AreNotSame(instance1, instance2);
        }

        [Test]
        public void Resolve_ScopedFromRootContainer_ThrowsException()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Scoped);
            var container = builder.Build();

            var ex = Assert.Throws<InvalidOperationException>(() => container.Resolve<ITestService>());
            Assert.That(ex.Message, Does.Contain("scoped"));
            Assert.That(ex.Message, Does.Contain("CreateScope"));
        }


        [Test]
        public void Resolve_WithDependencies_InjectsDependencies()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            builder.Register<ITestRepository, TestRepository>();
            builder.Register<ITestController, TestController>();
            var container = builder.Build();

            var controller = container.Resolve<ITestController>();

            Assert.IsNotNull(controller);
            var testController = (TestController)controller;
            Assert.IsNotNull(testController.Service);
            Assert.IsNotNull(testController.Repository);
            Assert.IsInstanceOf<TestService>(testController.Service);
            Assert.IsInstanceOf<TestRepository>(testController.Repository);
        }

        [Test]
        public void Resolve_WithParameterlessConstrutor_CreatesInstance()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, ServiceWithNoDependencies>();
            var container = builder.Build();

            var instance = container.Resolve<ITestService>();

            Assert.IsNotNull(instance);
            Assert.IsInstanceOf<ServiceWithNoDependencies>(instance);
        }

        [Test]
        public void Resolve_WithSingleDependency_InjectsDependency()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestRepository, TestRepository>();
            builder.Register<ITestService, ServiceWithDependency>();
            var container = builder.Build();

            var service = container.Resolve<ITestService>();

            Assert.IsNotNull(service);
            var serviceDep = (ServiceWithDependency)service;
            Assert.IsNotNull(serviceDep.Repository);
        }

        [Test]
        public void Resolve_WithMissingDependency_ThrowsException()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestController, TestController>();

            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.That(ex.Message, Does.Contain("not registered"));
        }


        [Test]
        public void Resolve_WithFactory_UsesFactory()
        {
            var builder = new ContainerBuilder();
            var customService = new TestService();
            builder.RegisterFactory<ITestService>(c => customService);
            var container = builder.Build();

            var instance = container.Resolve<ITestService>();

            Assert.AreSame(customService, instance);
        }

        [Test]
        public void Resolve_WithFactorySingleton_ReturnsSameInstance()
        {
            var builder = new ContainerBuilder();
            builder.RegisterFactory<ITestService>(c => new TestService(), Lifetime.Singleton);
            var container = builder.Build();

            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void Resolve_WithFactoryTransient_ReturnsDifferentInstances()
        {
            var builder = new ContainerBuilder();
            builder.RegisterFactory<ITestService>(c => new TestService(), Lifetime.Transient);
            var container = builder.Build();

            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            Assert.AreNotSame(instance1, instance2);
        }


        [Test]
        public void Resolve_WithRegisteredInstance_ReturnsInstance()
        {
            var builder = new ContainerBuilder();
            var customService = new TestService();
            builder.RegisterInstance<ITestService>(customService);
            var container = builder.Build();

            var instance = container.Resolve<ITestService>();

            Assert.AreSame(customService, instance);
        }

        [Test]
        public void Resolve_WithRegisteredInstance_AlwaysReturnsSameInstance()
        {
            var builder = new ContainerBuilder();
            var customService = new TestService();
            builder.RegisterInstance<ITestService>(customService);
            var container = builder.Build();

            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            Assert.AreSame(instance1, instance2);
            Assert.AreSame(customService, instance1);
        }


        [Test]
        public void TryResolve_WithRegisteredType_ReturnsTrue()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            var container = builder.Build();

            var success = container.TryResolve<ITestService>(out var instance);

            Assert.IsTrue(success);
            Assert.IsNotNull(instance);
        }

        [Test]
        public void TryResolve_WithUnregisteredType_ReturnsFalse()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();

            var success = container.TryResolve<ITestService>(out var instance);

            Assert.IsFalse(success);
            Assert.IsNull(instance);
        }


        [Test]
        public void IsRegistered_WithRegisteredType_ReturnsTrue()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            var container = builder.Build();

            var isRegistered = container.IsRegistered<ITestService>();

            Assert.IsTrue(isRegistered);
        }

        [Test]
        public void IsRegistered_WithUnregisteredType_ReturnsFalse()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();

            var isRegistered = container.IsRegistered<ITestService>();

            Assert.IsFalse(isRegistered);
        }

        [Test]
        public void IsRegistered_WithNullType_ThrowsException()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();

            Assert.Throws<ArgumentNullException>(() => container.IsRegistered(null));
        }


        [Test]
        public void CreateScope_ReturnsNewScope()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();

            var scope = container.CreateScope();

            Assert.IsNotNull(scope);
            Assert.AreSame(container, scope.Parent);
        }


        [Test]
        public void Dispose_DisposesAllSingletons()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, DisposableService>(Lifetime.Singleton);
            var container = builder.Build();
            var instance = (DisposableService)container.Resolve<ITestService>();

            container.Dispose();

            Assert.IsTrue(instance.IsDisposed);
        }

        [Test]
        public void Resolve_AfterDisposal_ThrowsException()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            var container = builder.Build();
            container.Dispose();

            Assert.Throws<ObjectDisposedException>(() => container.Resolve<ITestService>());
        }

        [Test]
        public void CreateScope_AfterDisposal_ThrowsException()
        {
            var builder = new ContainerBuilder();
            var container = builder.Build();
            container.Dispose();

            Assert.Throws<ObjectDisposedException>(() => container.CreateScope());
        }
    }
}
