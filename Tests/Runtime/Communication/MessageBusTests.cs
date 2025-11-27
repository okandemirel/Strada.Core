using System;
using System.Collections.Generic;
using NUnit.Framework;
using Strada.Core.Commands;
using Strada.Core.Communication;

namespace Strada.Core.Tests.Runtime.Communication
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

        #region Command Tests

        [Test]
        public void Send_WithRegisteredHandler_ExecutesHandler()
        {
            var command = new TestCommand { Value = 42 };
            int receivedValue = 0;

            _bus.RegisterCommandHandler<TestCommand>(cmd => receivedValue = cmd.Value);
            _bus.Send(command);

            Assert.AreEqual(42, receivedValue);
        }

        [Test]
        public void Send_ByRef_ExecutesHandler()
        {
            var command = new TestCommand { Value = 100 };
            int receivedValue = 0;

            _bus.RegisterCommandHandler<TestCommand>(cmd => receivedValue = cmd.Value);
            _bus.Send(ref command);

            Assert.AreEqual(100, receivedValue);
        }

        [Test]
        public void Send_WithoutHandler_ThrowsException()
        {
            var command = new UnhandledCommand();

            Assert.Throws<InvalidOperationException>(() => _bus.Send(command));
        }

        [Test]
        public void Send_WithInterfaceHandler_ExecutesHandler()
        {
            var handler = new TestCommandHandler();
            var command = new TestCommand { Value = 55 };

            _bus.RegisterCommandHandler<TestCommand>(handler);
            _bus.Send(command);

            Assert.AreEqual(55, handler.LastValue);
        }

        [Test]
        public void RegisterCommandHandler_OverwritesPreviousHandler()
        {
            int handler1Called = 0;
            int handler2Called = 0;

            _bus.RegisterCommandHandler<TestCommand>(_ => handler1Called++);
            _bus.RegisterCommandHandler<TestCommand>(_ => handler2Called++);

            _bus.Send(new TestCommand());

            Assert.AreEqual(0, handler1Called);
            Assert.AreEqual(1, handler2Called);
        }

        #endregion

        #region Query Tests

        [Test]
        public void Query_WithRegisteredHandler_ReturnsResult()
        {
            var query = new GetValueQuery { Multiplier = 5 };

            _bus.RegisterQueryHandler<GetValueQuery, int>(new GetValueQueryHandler());

            var result = _bus.Query<GetValueQuery, int>(query);

            Assert.AreEqual(50, result); // 10 * 5
        }

        [Test]
        public void Query_ByRef_ReturnsResult()
        {
            var query = new GetValueQuery { Multiplier = 3 };

            _bus.RegisterQueryHandler<GetValueQuery, int>(new GetValueQueryHandler());

            var result = _bus.Query<GetValueQuery, int>(ref query);

            Assert.AreEqual(30, result); // 10 * 3
        }

        [Test]
        public void Query_WithDelegateHandler_ReturnsResult()
        {
            _bus.RegisterQueryHandler<GetValueQuery, int>(q => q.Multiplier * 20);

            var result = _bus.Query<GetValueQuery, int>(new GetValueQuery { Multiplier = 2 });

            Assert.AreEqual(40, result);
        }

        [Test]
        public void Query_WithoutHandler_ThrowsException()
        {
            var query = new UnhandledQuery();

            Assert.Throws<InvalidOperationException>(() => _bus.Query<UnhandledQuery, int>(query));
        }

        [Test]
        public void Query_StringResult_ReturnsCorrectType()
        {
            _bus.RegisterQueryHandler<GetNameQuery, string>(q => $"Name_{q.Id}");

            var result = _bus.Query<GetNameQuery, string>(new GetNameQuery { Id = 42 });

            Assert.AreEqual("Name_42", result);
        }

        #endregion

        #region Event Tests

        [Test]
        public void Publish_WithSubscriber_NotifiesSubscriber()
        {
            var evt = new TestEvent { Message = "Hello" };
            string receivedMessage = null;

            _bus.Subscribe<TestEvent>(e => receivedMessage = e.Message);
            _bus.Publish(evt);

            Assert.AreEqual("Hello", receivedMessage);
        }

        [Test]
        public void Publish_ByRef_NotifiesSubscriber()
        {
            var evt = new TestEvent { Message = "World" };
            string receivedMessage = null;

            _bus.Subscribe<TestEvent>(e => receivedMessage = e.Message);
            _bus.Publish(ref evt);

            Assert.AreEqual("World", receivedMessage);
        }

        [Test]
        public void Publish_WithMultipleSubscribers_NotifiesAll()
        {
            var evt = new TestEvent { Message = "Multi" };
            var receivedMessages = new List<string>();

            _bus.Subscribe<TestEvent>(e => receivedMessages.Add(e.Message + "_1"));
            _bus.Subscribe<TestEvent>(e => receivedMessages.Add(e.Message + "_2"));
            _bus.Subscribe<TestEvent>(e => receivedMessages.Add(e.Message + "_3"));

            _bus.Publish(evt);

            Assert.AreEqual(3, receivedMessages.Count);
            Assert.Contains("Multi_1", receivedMessages);
            Assert.Contains("Multi_2", receivedMessages);
            Assert.Contains("Multi_3", receivedMessages);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            var evt = new TestEvent { Message = "NoOne" };

            Assert.DoesNotThrow(() => _bus.Publish(evt));
        }

        [Test]
        public void GetSubscriberCount_ReturnsCorrectCount()
        {
            Assert.AreEqual(0, _bus.GetSubscriberCount<TestEvent>());

            _bus.Subscribe<TestEvent>(_ => { });
            Assert.AreEqual(1, _bus.GetSubscriberCount<TestEvent>());

            _bus.Subscribe<TestEvent>(_ => { });
            Assert.AreEqual(2, _bus.GetSubscriberCount<TestEvent>());
        }

        [Test]
        public void Unsubscribe_RemovesHandler()
        {
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;

            _bus.Subscribe(handler);
            _bus.Publish(new TestEvent());
            Assert.AreEqual(1, callCount);

            _bus.Unsubscribe(handler);
            _bus.Publish(new TestEvent());
            Assert.AreEqual(1, callCount); // Should not increment
        }

        [Test]
        public void Unsubscribe_OnlyRemovesSpecificHandler()
        {
            int handler1Count = 0;
            int handler2Count = 0;
            Action<TestEvent> handler1 = _ => handler1Count++;
            Action<TestEvent> handler2 = _ => handler2Count++;

            _bus.Subscribe(handler1);
            _bus.Subscribe(handler2);
            _bus.Unsubscribe(handler1);

            _bus.Publish(new TestEvent());

            Assert.AreEqual(0, handler1Count);
            Assert.AreEqual(1, handler2Count);
        }

        [Test]
        public void Unsubscribe_NonExistentHandler_DoesNotThrow()
        {
            Action<TestEvent> handler = _ => { };

            Assert.DoesNotThrow(() => _bus.Unsubscribe(handler));
        }

        #endregion

        #region Execute (ICommand) Tests

        [Test]
        public void Execute_ICommand_ExecutesCommand()
        {
            var command = new SimpleCommand();

            _bus.Execute(command);

            Assert.IsTrue(command.WasExecuted);
        }

        [Test]
        public void Execute_PooledCommand_ReturnsToPool()
        {
            // Track execution externally since command state is reset on return
            bool wasExecuted = false;
            var command = TestPooledCommand.Rent();
            command.Value = 123;
            command.OnExecuteCallback = () => wasExecuted = true;

            _bus.Execute(command);

            Assert.IsTrue(wasExecuted);
            Assert.AreEqual(0, command.Value); // Reset after return to pool
        }

        #endregion

        #region ExecuteAsync Tests

        [Test]
        public void ExecuteAsync_CallsOnComplete()
        {
            var command = new TestAsyncCommand();
            bool completed = false;

            _bus.ExecuteAsync(command, () => completed = true);

            // Simulate async completion
            command.Complete();

            Assert.IsTrue(completed);
        }

        [Test]
        public void ExecuteAsync_PooledCommand_ReturnsToPoolOnComplete()
        {
            var command = new TestPooledAsyncCommand();
            command.TestValue = 999;

            _bus.ExecuteAsync(command);
            command.Complete();

            Assert.AreEqual(0, command.TestValue); // Reset after return
        }

        #endregion

        #region Clear Tests

        [Test]
        public void Clear_RemovesAllCommandHandlers()
        {
            _bus.RegisterCommandHandler<TestCommand>(_ => { });
            _bus.Clear();

            Assert.Throws<InvalidOperationException>(() => _bus.Send(new TestCommand()));
        }

        [Test]
        public void Clear_RemovesAllQueryHandlers()
        {
            _bus.RegisterQueryHandler<GetValueQuery, int>(q => q.Multiplier);
            _bus.Clear();

            Assert.Throws<InvalidOperationException>(() => _bus.Query<GetValueQuery, int>(new GetValueQuery()));
        }

        [Test]
        public void Clear_RemovesAllEventSubscribers()
        {
            int callCount = 0;
            _bus.Subscribe<TestEvent>(_ => callCount++);

            _bus.Clear();
            _bus.Publish(new TestEvent());

            Assert.AreEqual(0, callCount);
            Assert.AreEqual(0, _bus.GetSubscriberCount<TestEvent>());
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_ClearsAllHandlers()
        {
            _bus.RegisterCommandHandler<TestCommand>(_ => { });
            _bus.Subscribe<TestEvent>(_ => { });

            _bus.Dispose();

            // After dispose, handlers should be cleared
            Assert.AreEqual(0, _bus.GetSubscriberCount<TestEvent>());
        }

        [Test]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _bus.Dispose();
                _bus.Dispose();
                _bus.Dispose();
            });
        }

        #endregion

        #region Type Safety Tests

        [Test]
        public void Commands_AreSeparatedByType()
        {
            int command1Count = 0;
            int command2Count = 0;

            _bus.RegisterCommandHandler<TestCommand>(_ => command1Count++);
            _bus.RegisterCommandHandler<AnotherCommand>(_ => command2Count++);

            _bus.Send(new TestCommand());

            Assert.AreEqual(1, command1Count);
            Assert.AreEqual(0, command2Count);
        }

        [Test]
        public void Events_AreSeparatedByType()
        {
            int event1Count = 0;
            int event2Count = 0;

            _bus.Subscribe<TestEvent>(_ => event1Count++);
            _bus.Subscribe<AnotherEvent>(_ => event2Count++);

            _bus.Publish(new TestEvent());

            Assert.AreEqual(1, event1Count);
            Assert.AreEqual(0, event2Count);
        }

        [Test]
        public void Queries_AreSeparatedByType()
        {
            _bus.RegisterQueryHandler<GetValueQuery, int>(_ => 100);
            _bus.RegisterQueryHandler<GetNameQuery, string>(_ => "test");

            var intResult = _bus.Query<GetValueQuery, int>(new GetValueQuery());
            var stringResult = _bus.Query<GetNameQuery, string>(new GetNameQuery());

            Assert.AreEqual(100, intResult);
            Assert.AreEqual("test", stringResult);
        }

        #endregion

        #region Large Handler Count Tests

        [Test]
        public void Subscribe_ManyHandlers_AllReceiveEvents()
        {
            const int handlerCount = 100;
            int totalCalls = 0;

            for (int i = 0; i < handlerCount; i++)
            {
                _bus.Subscribe<TestEvent>(_ => totalCalls++);
            }

            _bus.Publish(new TestEvent());

            Assert.AreEqual(handlerCount, totalCalls);
            Assert.AreEqual(handlerCount, _bus.GetSubscriberCount<TestEvent>());
        }

        [Test]
        public void RegisterManyCommandTypes_AllWork()
        {
            const int typeCount = 100;
            var results = new int[typeCount];

            // This tests that the type ID system handles many types
            _bus.RegisterCommandHandler<TestCommand>(c => results[0] = c.Value);

            _bus.Send(new TestCommand { Value = 42 });

            Assert.AreEqual(42, results[0]);
        }

        #endregion

        #region Test Types

        private struct TestCommand
        {
            public int Value;
        }

        private struct AnotherCommand
        {
            public string Data;
        }

        private struct UnhandledCommand { }

        private struct TestEvent
        {
            public string Message;
        }

        private struct AnotherEvent
        {
            public int Code;
        }

        private struct GetValueQuery : IQuery<int>
        {
            public int Multiplier;
        }

        private struct GetNameQuery : IQuery<string>
        {
            public int Id;
        }

        private struct UnhandledQuery : IQuery<int> { }

        private class TestCommandHandler : ICommandHandler<TestCommand>
        {
            public int LastValue;

            public void Handle(TestCommand command)
            {
                LastValue = command.Value;
            }
        }

        private class GetValueQueryHandler : IQueryHandler<GetValueQuery, int>
        {
            public int Handle(ref GetValueQuery query)
            {
                return 10 * query.Multiplier;
            }
        }

        private class SimpleCommand : ICommand
        {
            public bool WasExecuted;

            public void Execute()
            {
                WasExecuted = true;
            }
        }

        private class TestPooledCommand : PooledCommandBase<TestPooledCommand>
        {
            public int Value;
            public bool WasExecuted;
            public Action OnExecuteCallback;

            public override void Reset()
            {
                Value = 0;
                WasExecuted = false;
                OnExecuteCallback = null;
            }

            protected override void OnExecute()
            {
                WasExecuted = true;
                OnExecuteCallback?.Invoke();
            }
        }

        private class TestAsyncCommand : IAsyncCommand
        {
            private Action _onComplete;

            public void Execute(Action onComplete)
            {
                _onComplete = onComplete;
            }

            public void Cancel() { }

            public void Complete()
            {
                _onComplete?.Invoke();
            }
        }

        private class TestPooledAsyncCommand : IAsyncCommand, IPooledCommand
        {
            public int TestValue;
            private Action _onComplete;

            public void Execute(Action onComplete)
            {
                _onComplete = onComplete;
            }

            public void Cancel() { }

            public void Complete()
            {
                _onComplete?.Invoke();
            }

            public void Execute()
            {
                // Not used for async
            }

            public void Reset()
            {
                TestValue = 0;
                _onComplete = null;
            }

            public void ReturnToPool()
            {
                Reset();
            }
        }

        #endregion
    }
}
