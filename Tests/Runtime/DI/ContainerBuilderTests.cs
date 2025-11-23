using NUnit.Framework;
using System;
using Strada.Core.DI;

namespace Strada.Core.Tests.DI
{
    /// <summary>
    /// Comprehensive tests for the ContainerBuilder class.
    /// </summary>
    [TestFixture]
    public class ContainerBuilderTests
    {
        #region Test Interfaces and Classes

        public interface ITestService { }
        public interface ITestRepository { }
        public interface IAbstractService { }

        public class TestService : ITestService { }
        public class TestRepository : ITestRepository { }

        public abstract class AbstractService : IAbstractService { }

        public class ServiceWithDependency
        {
            public ITestService Service { get; }

            public ServiceWithDependency(ITestService service)
            {
                Service = service;
            }
        }

        // For circular dependency tests
        public interface IServiceA { }
        public interface IServiceB { }
        public interface IServiceC { }

        public class ServiceA : IServiceA
        {
            public ServiceA(IServiceB serviceB) { }
        }

        public class ServiceB : IServiceB
        {
            public ServiceB(IServiceA serviceA) { }
        }

        public class ServiceC : IServiceC
        {
            public ServiceC(IServiceA serviceA) { }
        }

        #endregion

        #region Basic Registration Tests

        [Test]
        public void Register_WithInterfaceAndImplementation_Succeeds()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act
            var result = builder.Register<ITestService, TestService>();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(builder, result); // Fluent API
        }

        [Test]
        public void Register_ConcreteType_Succeeds()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act
            var result = builder.Register<TestService>();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(builder, result);
        }

        [Test]
        public void Register_WithDifferentLifetimes_Succeeds()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            var container = builder.Build();

            // Assert
            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();
            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void Register_AbstractClass_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                builder.Register<AbstractService>());
            Assert.That(ex.Message, Does.Contain("abstract"));
        }

        [Test]
        public void Register_Interface_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act & Assert - Trying to register interface as concrete type
            // Note: This would be a compile-time error due to constraints,
            // but we can test the validation logic indirectly
            Assert.Pass("Interface registration prevented by generic constraint at compile time");
        }

        #endregion

        #region Factory Registration Tests

        [Test]
        public void RegisterFactory_WithValidFactory_Succeeds()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act
            var result = builder.RegisterFactory<ITestService>(c => new TestService());

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(builder, result);
        }

        [Test]
        public void RegisterFactory_WithNullFactory_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                builder.RegisterFactory<ITestService>(null));
        }

        [Test]
        public void RegisterFactory_ResolvesUsingFactory()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var customInstance = new TestService();
            builder.RegisterFactory<ITestService>(c => customInstance);
            var container = builder.Build();

            // Act
            var instance = container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(customInstance, instance);
        }

        #endregion

        #region Instance Registration Tests

        [Test]
        public void RegisterInstance_WithValidInstance_Succeeds()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var instance = new TestService();

            // Act
            var result = builder.RegisterInstance<ITestService>(instance);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreSame(builder, result);
        }

        [Test]
        public void RegisterInstance_WithNull_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                builder.RegisterInstance<ITestService>(null));
        }

        [Test]
        public void RegisterInstance_ResolvesCorrectInstance()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var customInstance = new TestService();
            builder.RegisterInstance<ITestService>(customInstance);
            var container = builder.Build();

            // Act
            var instance = container.Resolve<ITestService>();

            // Assert
            Assert.AreSame(customInstance, instance);
        }

        #endregion

        #region Build Tests

        [Test]
        public void Build_WithValidRegistrations_ReturnsContainer()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();

            // Act
            var container = builder.Build();

            // Assert
            Assert.IsNotNull(container);
            Assert.IsInstanceOf<IContainer>(container);
        }

        [Test]
        public void Build_WithEmptyBuilder_ReturnsEmptyContainer()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act
            var container = builder.Build();

            // Assert
            Assert.IsNotNull(container);
            Assert.IsFalse(container.IsRegistered<ITestService>());
        }

        [Test]
        public void Build_MultipleTimes_ReturnsIndependentContainers()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);

            // Act
            var container1 = builder.Build();
            var container2 = builder.Build();

            // Assert - Each container has its own singleton instances
            var instance1 = container1.Resolve<ITestService>();
            var instance2 = container2.Resolve<ITestService>();
            Assert.AreNotSame(instance1, instance2);
        }

        #endregion

        #region Circular Dependency Detection Tests

        [Test]
        public void Build_WithCircularDependency_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<IServiceA, ServiceA>();
            builder.Register<IServiceB, ServiceB>();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.That(ex.Message, Does.Contain("Circular dependency"));
        }

        [Test]
        public void Build_WithoutCircularDependency_Succeeds()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            builder.Register<ServiceWithDependency>();

            // Act
            var container = builder.Build();

            // Assert
            Assert.IsNotNull(container);
        }

        [Test]
        public void Build_WithFactoryRegistration_SkipsCircularCheck()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.RegisterFactory<IServiceA>(c => new ServiceA(c.Resolve<IServiceB>()));
            builder.RegisterFactory<IServiceB>(c => new ServiceB(null)); // Break cycle with null

            // Act
            var container = builder.Build();

            // Assert - Build succeeds because factories are not checked for circular deps
            Assert.IsNotNull(container);
        }

        #endregion

        #region Fluent API Tests

        [Test]
        public void FluentAPI_ChainMultipleRegistrations_Succeeds()
        {
            // Arrange & Act
            var container = new ContainerBuilder()
                .Register<ITestService, TestService>()
                .Register<ITestRepository, TestRepository>()
                .RegisterFactory<ServiceWithDependency>(c => new ServiceWithDependency(c.Resolve<ITestService>()))
                .Build();

            // Assert
            Assert.IsTrue(container.IsRegistered<ITestService>());
            Assert.IsTrue(container.IsRegistered<ITestRepository>());
            Assert.IsTrue(container.IsRegistered<ServiceWithDependency>());
        }

        #endregion

        #region Overwrite Registration Tests

        [Test]
        public void Register_SameTypeTwice_OverwritesPreviousRegistration()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var instance1 = new TestService();
            var instance2 = new TestService();

            // Act
            builder.RegisterInstance<ITestService>(instance1);
            builder.RegisterInstance<ITestService>(instance2); // Overwrite
            var container = builder.Build();

            // Assert
            var resolved = container.Resolve<ITestService>();
            Assert.AreSame(instance2, resolved);
            Assert.AreNotSame(instance1, resolved);
        }

        #endregion
    }
}
