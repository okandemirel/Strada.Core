using System;
using NUnit.Framework;
using Strada.Core.Communication;

namespace Strada.Core.Tests.Communication
{
    public struct DamageEvent : IEventData
    {
        public int EntityId;
        public int Amount;
    }

    public struct HealEvent : IEventData
    {
        public int EntityId;
        public int Amount;
    }

    [TestFixture]
    public class ZeroAllocEventBusTests
    {
        private ZeroAllocEventBus _eventBus;

        [SetUp]
        public void Setup()
        {
            _eventBus = new ZeroAllocEventBus();
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus?.Dispose();
        }

        [Test]
        public void Subscribe_HandlerReceivesEvents()
        {
            int receivedAmount = 0;

            _eventBus.Subscribe<DamageEvent>(e => receivedAmount = e.Amount);
            _eventBus.Publish(new DamageEvent { EntityId = 1, Amount = 50 });

            Assert.AreEqual(50, receivedAmount);
        }

        [Test]
        public void Subscribe_MultipleHandlers_AllReceiveEvents()
        {
            int handler1Count = 0;
            int handler2Count = 0;

            _eventBus.Subscribe<DamageEvent>(_ => handler1Count++);
            _eventBus.Subscribe<DamageEvent>(_ => handler2Count++);
            _eventBus.Publish(new DamageEvent());

            Assert.AreEqual(1, handler1Count);
            Assert.AreEqual(1, handler2Count);
        }

        [Test]
        public void Unsubscribe_HandlerNoLongerReceivesEvents()
        {
            int count = 0;
            Action<DamageEvent> handler = _ => count++;

            _eventBus.Subscribe(handler);
            _eventBus.Publish(new DamageEvent());
            Assert.AreEqual(1, count);

            _eventBus.Unsubscribe(handler);
            _eventBus.Publish(new DamageEvent());
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Publish_DifferentEventTypes_OnlyMatchingHandlersCalled()
        {
            int damageCount = 0;
            int healCount = 0;

            _eventBus.Subscribe<DamageEvent>(_ => damageCount++);
            _eventBus.Subscribe<HealEvent>(_ => healCount++);

            _eventBus.Publish(new DamageEvent());
            _eventBus.Publish(new DamageEvent());
            _eventBus.Publish(new HealEvent());

            Assert.AreEqual(2, damageCount);
            Assert.AreEqual(1, healCount);
        }

        [Test]
        public void GetSubscriberCount_ReturnsCorrectCount()
        {
            _eventBus.Subscribe<DamageEvent>(_ => { });
            _eventBus.Subscribe<DamageEvent>(_ => { });
            _eventBus.Subscribe<HealEvent>(_ => { });

            Assert.AreEqual(2, _eventBus.GetSubscriberCount<DamageEvent>());
            Assert.AreEqual(1, _eventBus.GetSubscriberCount<HealEvent>());
        }

        [Test]
        public void Clear_RemovesAllHandlers()
        {
            _eventBus.Subscribe<DamageEvent>(_ => { });
            _eventBus.Subscribe<HealEvent>(_ => { });

            _eventBus.Clear();

            Assert.AreEqual(0, _eventBus.GetSubscriberCount<DamageEvent>());
            Assert.AreEqual(0, _eventBus.GetSubscriberCount<HealEvent>());
        }

        [Test]
        public void Publish_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _eventBus.Publish(new DamageEvent()));
        }

        [Test]
        public void PublishRef_PassesByReference()
        {
            var evt = new DamageEvent { Amount = 100 };

            _eventBus.Subscribe<DamageEvent>(e => Assert.AreEqual(100, e.Amount));
            _eventBus.Publish(ref evt);
        }
    }
}
