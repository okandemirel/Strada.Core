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
        #region Test Interfaces and Classes

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

        // For circular dependency testing
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

        #endregion

        #region Basic Resolution Tests

        [Test]
        public void Resolve_WithRegisteredType_ReturnsInstance()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            var container = builder.Build();

            // Act
            var instance = container.Resolve<ITestService>();

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOf<TestService>(instance);
        }

        [Test]
        public void Resolve_WithUnregisteredType_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => container.Resolve<ITestService>());
            Assert.That(ex.Message, Does.Contain("not registered"));
        }

        [Test]
        public void Resolve_WithNullType_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => container.Resolve(null));
        }

        [Test]
        public void Resolve_ConcreteType_ReturnsInstance()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ConcreteService>();
            var container = builder.Build();

            // Act
            var instance = container.Resolve<ConcreteService>();

            // Assert
            Assert.IsNotNull(instance);
            Assert.AreEqual("Test", instance.Name);
        }

        #endregion

        #region Lifetime Tests

        [Test]
        public void Resolve_Singleton_ReturnsSameInstance()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            var container = builder.Build();

            // Act
            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void Resolve_Transient_ReturnsDifferentInstances()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Transient);
            var container = builder.Build();

            // Act
            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            // Assert
            Assert.AreNotSame(instance1, instance2);
        }

        [Test]
        public void Resolve_ScopedFromRootContainer_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Scoped);
            var container = builder.Build();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => container.Resolve<ITestService>());
            Assert.That(ex.Message, Does.Contain("scoped"));
            Assert.That(ex.Message, Does.Contain("CreateScope"));
        }

        #endregion

        #region Constructor Injection Tests

        [Test]
        public void Resolve_WithDependencies_InjectsDependencies()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            builder.Register<ITestRepository, TestRepository>();
            builder.Register<ITestController, TestController>();
            var container = builder.Build();

            // Act
            var controller = container.Resolve<ITestController>();

            // Assert
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
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, ServiceWithNoDependencies>();
            var container = builder.Build();

            // Act
            var instance = container.Resolve<ITestService>();

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOf<ServiceWithNoDependencies>(instance);
        }

        [Test]
        public void Resolve_WithSingleDependency_InjectsDependency()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestRepository, TestRepository>();
            builder.Register<ITestService, ServiceWithDependency>();
            var container = builder.Build();

            // Act
            var service = container.Resolve<ITestService>();

            // Assert
            Assert.IsNotNull(service);
            var serviceDep = (ServiceWithDependency)service;
            Assert.IsNotNull(serviceDep.Repository);
        }

        [Test]
        public void Resolve_WithMissingDependency_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestController, TestController>(); // Missing ITestService and ITestRepository

            // Act & Assert - Early validation at build time (better practice)
            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.That(ex.Message, Does.Contain("not registered"));
        }

        #endregion

        #region Factory Registration Tests

        [Test]
        public void Resolve_WithFactory_UsesFactory()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var customService = new TestService();
            builder.RegisterFactory<ITestService>(c => customService);
            var container = builder.Build();

            // Act
            var instance = container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(customService, instance);
        }

        [Test]
        public void Resolve_WithFactorySingleton_ReturnsSameInstance()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterFactory<ITestService>(c => new TestService(), Lifetime.Singleton);
            var container = builder.Build();

            // Act
            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void Resolve_WithFactoryTransient_ReturnsDifferentInstances()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterFactory<ITestService>(c => new TestService(), Lifetime.Transient);
            var container = builder.Build();

            // Act
            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            // Assert
            Assert.AreNotSame(instance1, instance2);
        }

        #endregion

        #region Instance Registration Tests

        [Test]
        public void Resolve_WithRegisteredInstance_ReturnsInstance()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var customService = new TestService();
            builder.RegisterInstance<ITestService>(customService);
            var container = builder.Build();

            // Act
            var instance = container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(customService, instance);
        }

        [Test]
        public void Resolve_WithRegisteredInstance_AlwaysReturnsSameInstance()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var customService = new TestService();
            builder.RegisterInstance<ITestService>(customService);
            var container = builder.Build();

            // Act
            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(instance1, instance2);
            Assert.AreSame(customService, instance1);
        }

        #endregion

        #region TryResolve Tests

        [Test]
        public void TryResolve_WithRegisteredType_ReturnsTrue()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            var container = builder.Build();

            // Act
            var success = container.TryResolve<ITestService>(out var instance);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNotNull(instance);
        }

        [Test]
        public void TryResolve_WithUnregisteredType_ReturnsFalse()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();

            // Act
            var success = container.TryResolve<ITestService>(out var instance);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(instance);
        }

        #endregion

        #region IsRegistered Tests

        [Test]
        public void IsRegistered_WithRegisteredType_ReturnsTrue()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            var container = builder.Build();

            // Act
            var isRegistered = container.IsRegistered<ITestService>();

            // Assert
            Assert.IsTrue(isRegistered);
        }

        [Test]
        public void IsRegistered_WithUnregisteredType_ReturnsFalse()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();

            // Act
            var isRegistered = container.IsRegistered<ITestService>();

            // Assert
            Assert.IsFalse(isRegistered);
        }

        [Test]
        public void IsRegistered_WithNullType_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => container.IsRegistered(null));
        }

        #endregion

        #region Scope Tests

        [Test]
        public void CreateScope_ReturnsNewScope()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();

            // Act
            var scope = container.CreateScope();

            // Assert
            Assert.IsNotNull(scope);
            Assert.AreSame(container, scope.Parent);
        }

        #endregion

        #region Disposal Tests

        [Test]
        public void Dispose_DisposesAllSingletons()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, DisposableService>(Lifetime.Singleton);
            var container = builder.Build();
            var instance = (DisposableService)container.Resolve<ITestService>();

            // Act
            container.Dispose();

            // Assert
            Assert.IsTrue(instance.IsDisposed);
        }

        [Test]
        public void Resolve_AfterDisposal_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            var container = builder.Build();
            container.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => container.Resolve<ITestService>());
        }

        [Test]
        public void CreateScope_AfterDisposal_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();
            container.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => container.CreateScope());
        }

        #endregion
    }
}
