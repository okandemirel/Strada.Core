using System;
using NUnit.Framework;
using Strada.Core.DI;
using Strada.Core.Module;

namespace Strada.Core.Tests.Runtime.Modules
{
    [TestFixture]
    public class ModuleScopeTests
    {
        private IContainer _parentContainer;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.Register<ParentService>(Lifetime.Singleton);
            _parentContainer = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _parentContainer?.Dispose();
        }

        [Test]
        public void ModuleScope_ResolveFromParent_ReturnsParentInstance()
        {
            var scope = new ModuleScope(_parentContainer);

            var service = scope.Resolve<ParentService>();

            Assert.IsNotNull(service);

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_RegisterInstance_OverridesParent()
        {
            var scope = new ModuleScope(_parentContainer);
            var localService = new ParentService();

            scope.RegisterInstance(localService);
            var resolved = scope.Resolve<ParentService>();

            Assert.AreSame(localService, resolved);

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_RegisterFactory_CreatesOnFirstResolve()
        {
            var scope = new ModuleScope(_parentContainer);
            int createCount = 0;

            scope.RegisterFactory(() =>
            {
                createCount++;
                return new LocalService();
            });

            Assert.AreEqual(0, createCount);

            scope.Resolve<LocalService>();

            Assert.AreEqual(1, createCount);

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_RegisterFactory_CachesInstance()
        {
            var scope = new ModuleScope(_parentContainer);
            int createCount = 0;

            scope.RegisterFactory(() =>
            {
                createCount++;
                return new LocalService();
            });

            var first = scope.Resolve<LocalService>();
            var second = scope.Resolve<LocalService>();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, createCount);

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_TryResolve_ReturnsTrueForLocal()
        {
            var scope = new ModuleScope(_parentContainer);
            scope.RegisterInstance(new LocalService());

            bool found = scope.TryResolve<LocalService>(out var service);

            Assert.IsTrue(found);
            Assert.IsNotNull(service);

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_TryResolve_ReturnsTrueForParent()
        {
            var scope = new ModuleScope(_parentContainer);

            bool found = scope.TryResolve<ParentService>(out var service);

            Assert.IsTrue(found);
            Assert.IsNotNull(service);

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_IsRegistered_ReturnsTrueForLocal()
        {
            var scope = new ModuleScope(_parentContainer);
            scope.RegisterInstance(new LocalService());

            Assert.IsTrue(scope.IsRegistered<LocalService>());

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_IsRegistered_ReturnsTrueForParent()
        {
            var scope = new ModuleScope(_parentContainer);

            Assert.IsTrue(scope.IsRegistered<ParentService>());

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_CreateScope_ReturnsNestedScope()
        {
            var scope = new ModuleScope(_parentContainer);
            var nested = scope.CreateScope() as ModuleScope;

            Assert.IsNotNull(nested);

            nested.Dispose();
            scope.Dispose();
        }

        [Test]
        public void ModuleScope_NestedScope_ResolvesFromParentScope()
        {
            var scope = new ModuleScope(_parentContainer);
            scope.RegisterInstance(new LocalService());

            var nested = scope.CreateScope() as ModuleScope;
            var service = nested.Resolve<LocalService>();

            Assert.IsNotNull(service);

            nested.Dispose();
            scope.Dispose();
        }

        [Test]
        public void ModuleScope_Dispose_DisposesRegisteredInstances()
        {
            var scope = new ModuleScope(_parentContainer);
            var service = new DisposableService();
            scope.RegisterInstance(service);

            scope.Dispose();

            Assert.IsTrue(service.IsDisposed);
        }

        [Test]
        public void ModuleScope_Dispose_ClearsLocalInstances()
        {
            var scope = new ModuleScope(_parentContainer);
            scope.RegisterInstance(new LocalService());

            scope.Dispose();

            Assert.IsFalse(scope.IsRegistered<LocalService>());
        }

        [Test]
        public void ModuleScope_RegisterInterfaceFactory_ResolvesInterface()
        {
            var scope = new ModuleScope(_parentContainer);

            scope.RegisterFactory<ILocalService, LocalService>(() => new LocalService());

            var service = scope.Resolve<ILocalService>();

            Assert.IsNotNull(service);
            Assert.IsInstanceOf<LocalService>(service);

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_ResolveByType_ReturnsInstance()
        {
            var scope = new ModuleScope(_parentContainer);
            scope.RegisterInstance(new LocalService());

            var service = scope.Resolve(typeof(LocalService));

            Assert.IsNotNull(service);
            Assert.IsInstanceOf<LocalService>(service);

            scope.Dispose();
        }

        [Test]
        public void ModuleScope_Parent_ExposesParentContainer()
        {
            var scope = new ModuleScope(_parentContainer);

            Assert.AreSame(_parentContainer, scope.Parent);

            scope.Dispose();
        }

        private class ParentService { }

        private interface ILocalService { }

        private class LocalService : ILocalService { }

        private class DisposableService : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }
    }
}
