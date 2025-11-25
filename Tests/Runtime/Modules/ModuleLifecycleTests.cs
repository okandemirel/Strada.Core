using System;
using System.Collections.Generic;
using NUnit.Framework;
using Strada.Core.DI;
using Strada.Core.Module;

namespace Strada.Core.Tests.Runtime.Modules
{
    [TestFixture]
    public class ModuleLifecycleTests
    {
        private IContainer _container;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
        }

        [Test]
        public void LifecycleModule_InitialPhase_IsNone()
        {
            var module = new TestModule();

            Assert.AreEqual(ModulePhase.None, module.Phase);
        }

        [Test]
        public void LifecycleModule_PreInitialize_SetsPhase()
        {
            var module = new TestModule();

            module.PreInitialize(_container);

            Assert.AreEqual(ModulePhase.PreInitialize, module.Phase);
        }

        [Test]
        public void LifecycleModule_PreInitialize_CallsOnPreInitialize()
        {
            var module = new TestModule();

            module.PreInitialize(_container);

            Assert.IsTrue(module.PreInitializeCalled);
        }

        [Test]
        public void LifecycleModule_Initialize_RequiresPreInitialize()
        {
            var module = new TestModule();

            module.Initialize(_container);

            Assert.AreEqual(ModulePhase.None, module.Phase);
            Assert.IsFalse(module.InitializeCalled);
        }

        [Test]
        public void LifecycleModule_Initialize_AfterPreInitialize_Succeeds()
        {
            var module = new TestModule();
            module.PreInitialize(_container);

            module.Initialize(_container);

            Assert.AreEqual(ModulePhase.Initialize, module.Phase);
            Assert.IsTrue(module.InitializeCalled);
        }

        [Test]
        public void LifecycleModule_PostInitialize_AfterInitialize_Succeeds()
        {
            var module = new TestModule();
            module.PreInitialize(_container);
            module.Initialize(_container);

            module.PostInitialize(_container);

            Assert.AreEqual(ModulePhase.Ready, module.Phase);
            Assert.IsTrue(module.PostInitializeCalled);
        }

        [Test]
        public void LifecycleModule_FullLifecycle_AllPhasesInOrder()
        {
            var module = new TestModule();

            module.PreInitialize(_container);
            Assert.AreEqual(ModulePhase.PreInitialize, module.Phase);

            module.Initialize(_container);
            Assert.AreEqual(ModulePhase.Initialize, module.Phase);

            module.PostInitialize(_container);
            Assert.AreEqual(ModulePhase.Ready, module.Phase);

            module.Shutdown();
            Assert.AreEqual(ModulePhase.Shutdown, module.Phase);
        }

        [Test]
        public void LifecycleModule_Shutdown_CallsOnShutdown()
        {
            var module = new TestModule();
            module.PreInitialize(_container);
            module.Initialize(_container);
            module.PostInitialize(_container);

            module.Shutdown();

            Assert.IsTrue(module.ShutdownCalled);
        }

        [Test]
        public void LifecycleModule_Dispose_SetsPhaseToDisposed()
        {
            var module = new TestModule();
            module.PreInitialize(_container);
            module.Initialize(_container);
            module.PostInitialize(_container);

            module.Dispose();

            Assert.AreEqual(ModulePhase.Disposed, module.Phase);
        }

        [Test]
        public void LifecycleModule_Dispose_CallsShutdownFirst()
        {
            var module = new TestModule();
            module.PreInitialize(_container);
            module.Initialize(_container);
            module.PostInitialize(_container);

            module.Dispose();

            Assert.IsTrue(module.ShutdownCalled);
            Assert.IsTrue(module.DisposeCalled);
        }

        [Test]
        public void LifecycleModule_Dispose_CalledOnce()
        {
            var module = new TestModule();
            module.PreInitialize(_container);

            module.Dispose();
            module.Dispose();

            Assert.AreEqual(1, module.DisposeCallCount);
        }

        [Test]
        public void LifecycleModule_RegisterLocal_CanBeResolved()
        {
            var module = new RegisteringModule();
            module.PreInitialize(_container);
            module.Initialize(_container);

            var resolved = module.ResolveLocalService();

            Assert.IsNotNull(resolved);

            module.Dispose();
        }

        [Test]
        public void LifecycleModule_Name_ReturnsModuleName()
        {
            var module = new TestModule();

            Assert.AreEqual("TestModule", module.Name);
        }

        [Test]
        public void LifecycleModule_Priority_ReturnsDefault()
        {
            var module = new TestModule();

            Assert.AreEqual(0, module.Priority);
        }

        [Test]
        public void LifecycleModule_Dependencies_ReturnsEmpty()
        {
            var module = new TestModule();

            Assert.IsEmpty(module.Dependencies);
        }

        [Test]
        public void LifecycleModule_PriorityModule_ReturnsPriority()
        {
            var module = new PriorityModule();

            Assert.AreEqual(100, module.Priority);
        }

        [Test]
        public void LifecycleModule_DependentModule_ReturnsDependencies()
        {
            var module = new DependentModule();

            CollectionAssert.Contains(module.Dependencies, typeof(TestModule));
        }

        private class TestModule : LifecycleModule
        {
            public bool PreInitializeCalled;
            public bool InitializeCalled;
            public bool PostInitializeCalled;
            public bool ShutdownCalled;
            public bool DisposeCalled;
            public int DisposeCallCount;

            public override string Name => "TestModule";

            protected override void OnPreInitialize() => PreInitializeCalled = true;
            protected override void OnInitialize() => InitializeCalled = true;
            protected override void OnPostInitialize() => PostInitializeCalled = true;
            protected override void OnShutdown() => ShutdownCalled = true;

            protected override void OnDispose()
            {
                DisposeCalled = true;
                DisposeCallCount++;
            }
        }

        private class PriorityModule : LifecycleModule
        {
            public override string Name => "PriorityModule";
            public override int Priority => 100;
        }

        private class DependentModule : LifecycleModule
        {
            public override string Name => "DependentModule";
            public override IEnumerable<Type> Dependencies => new[] { typeof(TestModule) };
        }

        private class LocalService { }

        private class RegisteringModule : LifecycleModule
        {
            public override string Name => "RegisteringModule";

            protected override void OnInitialize()
            {
                RegisterLocal(new LocalService());
            }

            public LocalService ResolveLocalService() => Resolve<LocalService>();
        }
    }
}
