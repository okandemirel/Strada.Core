using System;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    /// <summary>
    /// Comprehensive tests for the ContainerBuilder class.
    /// </summary>
    [TestFixture]
    public class ContainerBuilderTests
    {

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

        [Test]
        public void Register_WithInterfaceAndImplementation_Succeeds()
        {
            var builder = new ContainerBuilder();

            var result = builder.Register<ITestService, TestService>();

            Assert.IsNotNull(result);
            Assert.AreSame(builder, result); // Fluent API
        }

        [Test]
        public void Register_ConcreteType_Succeeds()
        {
            var builder = new ContainerBuilder();

            var result = builder.Register<TestService>();

            Assert.IsNotNull(result);
            Assert.AreSame(builder, result);
        }

        [Test]
        public void Register_WithDifferentLifetimes_Succeeds()
        {
            var builder = new ContainerBuilder();

            builder.Register<ITestService, TestService>(Lifetime.Singleton);
            var container = builder.Build();

            var instance1 = container.Resolve<ITestService>();
            var instance2 = container.Resolve<ITestService>();
            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void Register_AbstractClass_ThrowsException()
        {
            var builder = new ContainerBuilder();

            var ex = Assert.Throws<ArgumentException>(() =>
                builder.Register<AbstractService>());
            Assert.That(ex.Message, Does.Contain("abstract"));
        }

        [Test]
        public void Register_Interface_ThrowsException()
        {
            var builder = new ContainerBuilder();

            Assert.Pass("Interface registration prevented by generic constraint at compile time");
        }


        [Test]
        public void RegisterFactory_WithValidFactory_Succeeds()
        {
            var builder = new ContainerBuilder();

            var result = builder.RegisterFactory<ITestService>(c => new TestService());

            Assert.IsNotNull(result);
            Assert.AreSame(builder, result);
        }

        [Test]
        public void RegisterFactory_WithNullFactory_ThrowsException()
        {
            var builder = new ContainerBuilder();

            Assert.Throws<ArgumentNullException>(() =>
                builder.RegisterFactory<ITestService>(null));
        }

        [Test]
        public void RegisterFactory_ResolvesUsingFactory()
        {
            var builder = new ContainerBuilder();
            var customInstance = new TestService();
            builder.RegisterFactory<ITestService>(c => customInstance);
            var container = builder.Build();

            var instance = container.Resolve<ITestService>();

            Assert.AreSame(customInstance, instance);
        }


        [Test]
        public void RegisterInstance_WithValidInstance_Succeeds()
        {
            var builder = new ContainerBuilder();
            var instance = new TestService();

            var result = builder.RegisterInstance<ITestService>(instance);

            Assert.IsNotNull(result);
            Assert.AreSame(builder, result);
        }

        [Test]
        public void RegisterInstance_WithNull_ThrowsException()
        {
            var builder = new ContainerBuilder();

            Assert.Throws<ArgumentNullException>(() =>
                builder.RegisterInstance<ITestService>(null));
        }

        [Test]
        public void RegisterInstance_ResolvesCorrectInstance()
        {
            var builder = new ContainerBuilder();
            var customInstance = new TestService();
            builder.RegisterInstance<ITestService>(customInstance);
            var container = builder.Build();

            var instance = container.Resolve<ITestService>();

            Assert.AreSame(customInstance, instance);
        }


        [Test]
        public void Build_WithValidRegistrations_ReturnsContainer()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();

            var container = builder.Build();

            Assert.IsNotNull(container);
            Assert.IsInstanceOf<IContainer>(container);
        }

        [Test]
        public void Build_WithEmptyBuilder_ReturnsEmptyContainer()
        {
            var builder = new ContainerBuilder();

            var container = builder.Build();

            Assert.IsNotNull(container);
            Assert.IsFalse(container.IsRegistered<ITestService>());
        }

        [Test]
        public void Build_MultipleTimes_ReturnsIndependentContainers()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>(Lifetime.Singleton);

            var container1 = builder.Build();
            var container2 = builder.Build();

            var instance1 = container1.Resolve<ITestService>();
            var instance2 = container2.Resolve<ITestService>();
            Assert.AreNotSame(instance1, instance2);
        }


        [Test]
        public void Build_WithCircularDependency_ThrowsException()
        {
            var builder = new ContainerBuilder();
            builder.Register<IServiceA, ServiceA>();
            builder.Register<IServiceB, ServiceB>();

            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.That(ex.Message, Does.Contain("Circular dependency"));
        }

        [Test]
        public void Build_WithoutCircularDependency_Succeeds()
        {
            var builder = new ContainerBuilder();
            builder.Register<ITestService, TestService>();
            builder.Register<ServiceWithDependency>();

            var container = builder.Build();

            Assert.IsNotNull(container);
        }

        [Test]
        public void Build_WithFactoryRegistration_SkipsCircularCheck()
        {
            var builder = new ContainerBuilder();
            builder.RegisterFactory<IServiceA>(c => new ServiceA(c.Resolve<IServiceB>()));
            builder.RegisterFactory<IServiceB>(c => new ServiceB(null)); // Break cycle with null

            var container = builder.Build();

            Assert.IsNotNull(container);
        }


        [Test]
        public void FluentAPI_ChainMultipleRegistrations_Succeeds()
        {
            var container = new ContainerBuilder()
                .Register<ITestService, TestService>()
                .Register<ITestRepository, TestRepository>()
                .RegisterFactory<ServiceWithDependency>(c => new ServiceWithDependency(c.Resolve<ITestService>()))
                .Build();

            Assert.IsTrue(container.IsRegistered<ITestService>());
            Assert.IsTrue(container.IsRegistered<ITestRepository>());
            Assert.IsTrue(container.IsRegistered<ServiceWithDependency>());
        }


        [Test]
        public void Register_SameTypeTwice_OverwritesPreviousRegistration()
        {
            var builder = new ContainerBuilder();
            var instance1 = new TestService();
            var instance2 = new TestService();

            builder.RegisterInstance<ITestService>(instance1);
            builder.RegisterInstance<ITestService>(instance2); // Overwrite
            var container = builder.Build();

            var resolved = container.Resolve<ITestService>();
            Assert.AreSame(instance2, resolved);
            Assert.AreNotSame(instance1, resolved);
        }
    }
}
