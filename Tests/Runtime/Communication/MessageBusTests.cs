using System;
using NUnit.Framework;
using Strada.Core.Communication;

namespace Strada.Core.Tests.Communication
{
    [TestFixture]
    public class MessageBusTests
    {
        private MessageBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new MessageBus();
        }

        [TearDown]
        public void TearDown()
        {
            _bus?.Dispose();
        }

        [Test]
        public void Send_WithRegisteredHandler_ExecutesHandler()
        {
            var executed = false;
            _bus.RegisterCommandHandler<TestCommand>(cmd => executed = true);

            var command = new TestCommand { Value = 42 };
            _bus.Send(ref command);

            Assert.IsTrue(executed);
        }

        [Test]
        public void Send_WithValue_PassesCorrectData()
        {
            var receivedValue = 0;
            _bus.RegisterCommandHandler<TestCommand>(cmd => receivedValue = cmd.Value);

            var command = new TestCommand { Value = 123 };
            _bus.Send(ref command);

            Assert.AreEqual(123, receivedValue);
        }

        [Test]
        public void Send_WithoutHandler_ThrowsException()
        {
            var command = new TestCommand();
            Assert.Throws<InvalidOperationException>(() => _bus.Send(ref command));
        }

        [Test]
        public void Query_WithRegisteredHandler_ReturnsResult()
        {
            _bus.RegisterQueryHandler<TestQuery, int>(q => q.Input * 2);

            var query = new TestQuery { Input = 21 };
            var result = _bus.Query<TestQuery, int>(ref query);

            Assert.AreEqual(42, result);
        }

        [Test]
        public void Query_WithoutHandler_ThrowsException()
        {
            var query = new TestQuery();
            Assert.Throws<InvalidOperationException>(() => _bus.Query<TestQuery, int>(ref query));
        }

        [Test]
        public void Publish_WithSubscriber_NotifiesSubscriber()
        {
            var received = false;
            _bus.Subscribe<TestEvent>(evt => received = true);

            var evt = new TestEvent();
            _bus.Publish(ref evt);

            Assert.IsTrue(received);
        }

        [Test]
        public void Publish_WithMultipleSubscribers_NotifiesAll()
        {
            var count = 0;
            _bus.Subscribe<TestEvent>(evt => count++);
            _bus.Subscribe<TestEvent>(evt => count++);
            _bus.Subscribe<TestEvent>(evt => count++);

            var evt = new TestEvent();
            _bus.Publish(ref evt);

            Assert.AreEqual(3, count);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            var evt = new TestEvent();
            Assert.DoesNotThrow(() => _bus.Publish(ref evt));
        }

        [Test]
        public void Unsubscribe_RemovesHandler()
        {
            var count = 0;
            Action<TestEvent> handler = evt => count++;

            _bus.Subscribe(handler);
            _bus.Publish(new TestEvent());
            Assert.AreEqual(1, count);

            _bus.Unsubscribe(handler);
            _bus.Publish(new TestEvent());
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Publish_PreservesEventData()
        {
            var receivedValue = 0;
            _bus.Subscribe<TestEvent>(evt => receivedValue = evt.Data);

            var evt = new TestEvent { Data = 999 };
            _bus.Publish(ref evt);

            Assert.AreEqual(999, receivedValue);
        }

        [Test]
        public void MultipleTypes_IndependentHandling()
        {
            var commandExecuted = false;
            var eventReceived = false;

            _bus.RegisterCommandHandler<TestCommand>(cmd => commandExecuted = true);
            _bus.Subscribe<TestEvent>(evt => eventReceived = true);

            var cmd = new TestCommand();
            _bus.Send(ref cmd);

            Assert.IsTrue(commandExecuted);
            Assert.IsFalse(eventReceived);

            var evt = new TestEvent();
            _bus.Publish(ref evt);

            Assert.IsTrue(eventReceived);
        }

        [Test]
        public void Dispose_ClearsAllHandlers()
        {
            _bus.RegisterCommandHandler<TestCommand>(cmd => { });
            _bus.Subscribe<TestEvent>(evt => { });

            _bus.Dispose();

            var cmd = new TestCommand();
            Assert.Throws<InvalidOperationException>(() => _bus.Send(ref cmd));
        }

        private struct TestCommand
        {
            public int Value;
        }

        private struct TestQuery : IQuery<int>
        {
            public int Input;
        }

        private struct TestEvent
        {
            public int Data;
        }
    }
}
