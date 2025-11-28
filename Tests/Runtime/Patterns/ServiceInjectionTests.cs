using NUnit.Framework;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.Patterns;

namespace Strada.Core.Tests.Tests.Runtime.Patterns
{
    [TestFixture]
    public class ServiceInjectionTests
    {
        private IContainer _container;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.Register<MessageBus>(Lifetime.Singleton);
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
        }

        [Test]
        public void Service_Initialize_SetsIsInitialized()
        {
            var service = new TestService();
            InjectionProcessor.Inject(service, _container);

            Assert.IsFalse(service.IsInit);

            service.Initialize();

            Assert.IsTrue(service.IsInit);
        }

        [Test]
        public void Service_Initialize_CalledOnce()
        {
            var service = new TestService();
            InjectionProcessor.Inject(service, _container);

            service.Initialize();
            service.Initialize();
            service.Initialize();

            Assert.AreEqual(1, service.InitCount);
        }

        [Test]
        public void Service_Dispose_CalledOnce()
        {
            var service = new TestService();
            InjectionProcessor.Inject(service, _container);
            service.Initialize();

            service.Dispose();
            service.Dispose();

            Assert.AreEqual(1, service.DisposeCount);
        }

        [Test]
        public void Service_Inject_ResolvesContainer()
        {
            var service = new TestService();
            InjectionProcessor.Inject(service, _container);

            Assert.IsNotNull(service.InjectedContainer);
        }

        [Test]
        public void Service_Inject_ResolvesBus()
        {
            var service = new TestService();
            InjectionProcessor.Inject(service, _container);

            Assert.IsNotNull(service.InjectedBus);
        }

        [Test]
        public void TickableService_ImplementsTick()
        {
            var service = new TestTickableService();
            InjectionProcessor.Inject(service, _container);
            service.Initialize();

            service.Tick(0.016f);

            Assert.AreEqual(1, service.TickCount);
            Assert.AreEqual(0.016f, service.LastDeltaTime, 0.0001f);
        }

        [Test]
        public void FixedTickableService_ImplementsFixedTick()
        {
            var service = new TestFixedTickableService();
            InjectionProcessor.Inject(service, _container);
            service.Initialize();

            service.FixedTick(0.02f);

            Assert.AreEqual(1, service.FixedTickCount);
            Assert.AreEqual(0.02f, service.LastFixedDeltaTime, 0.0001f);
        }

        private class TestService : Service
        {
            public int InitCount;
            public int DisposeCount;
            public bool IsInit => IsInitialized;
            public IContainer InjectedContainer => Container;
            public MessageBus InjectedBus => MessageBus;

            protected override void OnInitialize()
            {
                InitCount++;
            }

            protected override void OnDispose()
            {
                DisposeCount++;
            }
        }

        private class TestTickableService : TickableService
        {
            public int TickCount;
            public float LastDeltaTime;

            public override void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }
        }

        private class TestFixedTickableService : FixedTickableService
        {
            public int FixedTickCount;
            public float LastFixedDeltaTime;

            public override void FixedTick(float fixedDeltaTime)
            {
                FixedTickCount++;
                LastFixedDeltaTime = fixedDeltaTime;
            }
        }
    }
}
