using NUnit.Framework;
using System;
using Strada.Core.DI;

namespace Strada.Core.Tests.DI
{
    /// <summary>
    /// Comprehensive tests for the ContainerScope class.
    /// </summary>
    [TestFixture]
    public class ContainerScopeTests
    {
        #region Test Interfaces and Classes

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

        #endregion

        #region Basic Scope Tests

        [Test]
        public void CreateScope_ReturnsValidScope()
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

        [Test]
        public void CreateScope_MultipleTimes_ReturnsDifferentScopes()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();

            // Act
            var scope1 = container.CreateScope();
            var scope2 = container.CreateScope();

            // Assert
            Assert.AreNotSame(scope1, scope2);
            Assert.AreSame(container, scope1.Parent);
            Assert.AreSame(container, scope2.Parent);
        }

        [Test]
        public void CreateScope_FromScope_CreatesChildScope()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var parentScope = container.CreateScope();

            // Act
            var childScope = parentScope.CreateScope();

            // Assert
            Assert.IsNotNull(childScope);
            Assert.AreSame(container, childScope.Parent); // Parent is still root container
        }

        #endregion

        #region Scoped Lifetime Tests

        [Test]
        public void Resolve_ScopedService_ReturnsSameInstanceWithinScope()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            // Act
            var instance1 = scope.Resolve<IScopedService>();
            var instance2 = scope.Resolve<IScopedService>();

            // Assert
            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void Resolve_ScopedService_ReturnsDifferentInstancesBetweenScopes()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope1 = container.CreateScope();
            var scope2 = container.CreateScope();

            // Act
            var instance1 = scope1.Resolve<IScopedService>();
            var instance2 = scope2.Resolve<IScopedService>();

            // Assert
            Assert.AreNotSame(instance1, instance2);
        }

        [Test]
        public void Resolve_SingletonFromScope_ReturnsSameInstanceAsParent()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            var container = builder.Build();
            var scope = container.CreateScope();

            // Act
            var containerInstance = container.Resolve<ITestService>();
            var scopeInstance = scope.Resolve<ITestService>();

            // Assert
            Assert.AreSame(containerInstance, scopeInstance);
        }

        [Test]
        public void Resolve_TransientFromScope_ReturnsDifferentInstances()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Transient);
            var container = builder.Build();
            var scope = container.CreateScope();

            // Act
            var instance1 = scope.Resolve<ITestService>();
            var instance2 = scope.Resolve<ITestService>();

            // Assert
            Assert.AreNotSame(instance1, instance2);
        }

        #endregion

        #region Scoped Dependencies Tests

        [Test]
        public void Resolve_ScopedWithDependencies_InjectsDependencies()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            builder.Register<IScopedService, ServiceWithDependency>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            // Act
            var instance = scope.Resolve<IScopedService>();

            // Assert
            var serviceDep = (ServiceWithDependency)instance;
            Assert.IsNotNull(serviceDep.Service);
        }

        #endregion

        #region Resolution Tests

        [Test]
        public void Resolve_UnregisteredType_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var scope = container.CreateScope();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => scope.Resolve<ITestService>());
            Assert.That(ex.Message, Does.Contain("not registered"));
        }

        [Test]
        public void TryResolve_FromScope_Works()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            // Act
            var success = scope.TryResolve<IScopedService>(out var instance);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNotNull(instance);
        }

        [Test]
        public void IsRegistered_FromScope_Works()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();

            // Act
            var isRegistered = scope.IsRegistered<IScopedService>();

            // Assert
            Assert.IsTrue(isRegistered);
        }

        #endregion

        #region Disposal Tests

        [Test]
        public void Dispose_DisposesAllScopedInstances()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, DisposableService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();
            var instance = (DisposableService)scope.Resolve<IScopedService>();

            // Act
            scope.Dispose();

            // Assert
            Assert.IsTrue(instance.IsDisposed);
        }

        [Test]
        public void Dispose_DoesNotDisposeParentSingletons()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            var container = builder.Build();
            var scope = container.CreateScope();
            scope.Resolve<ITestService>(); // Resolve from scope

            // Act
            scope.Dispose();

            // Assert - Should still be able to resolve from parent
            var instance = container.Resolve<ITestService>();
            Assert.IsNotNull(instance);
        }

        [Test]
        public void Resolve_AfterDisposal_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            builder.Register<IScopedService, ScopedService>(Lifetime.Scoped);
            var container = builder.Build();
            var scope = container.CreateScope();
            scope.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => scope.Resolve<IScopedService>());
        }

        [Test]
        public void CreateScope_AfterDisposal_ThrowsException()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var scope = container.CreateScope();
            scope.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
        }

        [Test]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var builder = new ContainerBuilder();
            var container = builder.Build();
            var scope = container.CreateScope();

            // Act & Assert - Should not throw
            scope.Dispose();
            Assert.DoesNotThrow(() => scope.Dispose());
        }

        #endregion
    }
}
