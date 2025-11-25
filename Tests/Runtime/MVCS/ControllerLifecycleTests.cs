using System;
using NUnit.Framework;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.DI.Attributes;
using Strada.Core.MVCS;
using Strada.Core.MVCS.Interfaces;

namespace Strada.Core.Tests.Runtime.MVCS
{
    [TestFixture]
    public class ControllerLifecycleTests
    {
        private IContainer _container;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.Register<ZeroAllocEventBus>(Lifetime.Singleton);
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
        }

        [Test]
        public void Controller_Initialize_SetsIsInitialized()
        {
            var controller = new TestController();
            InjectionProcessor.Inject(controller, _container);

            Assert.IsFalse(controller.IsInit);

            controller.Initialize();

            Assert.IsTrue(controller.IsInit);
        }

        [Test]
        public void Controller_Initialize_CalledOnce()
        {
            var controller = new TestController();
            InjectionProcessor.Inject(controller, _container);

            controller.Initialize();
            controller.Initialize();
            controller.Initialize();

            Assert.AreEqual(1, controller.InitCount);
        }

        [Test]
        public void Controller_Dispose_CalledOnce()
        {
            var controller = new TestController();
            InjectionProcessor.Inject(controller, _container);
            controller.Initialize();

            controller.Dispose();
            controller.Dispose();

            Assert.AreEqual(1, controller.DisposeCount);
        }

        [Test]
        public void Controller_Inject_ResolvesContainer()
        {
            var controller = new TestController();
            InjectionProcessor.Inject(controller, _container);

            Assert.IsNotNull(controller.InjectedContainer);
        }

        [Test]
        public void Controller_Subscribe_AutoUnsubscribesOnDispose()
        {
            var controller = new SubscribingController();
            InjectionProcessor.Inject(controller, _container);
            controller.Initialize();

            var eventBus = _container.Resolve<ZeroAllocEventBus>();
            int beforeCount = eventBus.GetSubscriberCount<TestEvent>();

            controller.Dispose();

            int afterCount = eventBus.GetSubscriberCount<TestEvent>();
            Assert.Less(afterCount, beforeCount);
        }

        [Test]
        public void Controller_WithModel_InjectsModel()
        {
            var model = new TestModel();
            var builder = new ContainerBuilder();
            builder.Register<TestModel>(Lifetime.Singleton);
            builder.RegisterInstance(model);
            var container = builder.Build();

            var controller = new ModelController();
            InjectionProcessor.Inject(controller, container);

            Assert.AreSame(model, controller.GetModel());

            container.Dispose();
        }

        private class TestController : StradaController
        {
            public int InitCount;
            public int DisposeCount;
            public bool IsInit => IsInitialized;
            public IContainer InjectedContainer => Container;

            protected override void OnInitialize()
            {
                InitCount++;
            }

            protected override void OnDispose()
            {
                DisposeCount++;
            }
        }

        private struct TestEvent : IEventData { }

        private class SubscribingController : StradaController
        {
            protected override void OnInitialize()
            {
                Subscribe<TestEvent>(OnTestEvent);
            }

            private void OnTestEvent(TestEvent evt) { }
        }

        private class TestModel : StradaModel { }

        private class ModelController : StradaController<TestModel>
        {
            public TestModel GetModel() => Model;
        }
    }
}
