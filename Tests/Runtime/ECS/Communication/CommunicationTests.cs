using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Communication;
using System;
using System.Collections.Generic;

namespace Strada.Core.Tests.ECS.Communication
{
    /// <summary>
    /// Tests for MVCS↔ECS communication bridge.
    /// </summary>
    [TestFixture]
    public class CommunicationTests
    {
        #region Command Buffer Tests

        [Test]
        public void CommandBuffer_Send_AddsCommand()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            var command = new TestCommand { Value = 42 };

            // Act
            buffer.Send(command);

            // Assert
            Assert.AreEqual(1, buffer.PendingCount);
        }

        [Test]
        public void CommandBuffer_GetCommands_RetrievesAll()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            buffer.Send(new TestCommand { Value = 1 });
            buffer.Send(new TestCommand { Value = 2 });
            buffer.Send(new TestCommand { Value = 3 });

            // Act
            var commands = buffer.GetCommands<TestCommand>();

            // Assert
            Assert.AreEqual(3, commands.Length);
            Assert.AreEqual(1, commands[0].Value);
            Assert.AreEqual(2, commands[1].Value);
            Assert.AreEqual(3, commands[2].Value);
        }

        [Test]
        public void CommandBuffer_GetCommands_ClearsQueue()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            buffer.Send(new TestCommand { Value = 1 });

            // Act
            buffer.GetCommands<TestCommand>();

            // Assert
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void CommandBuffer_GetCommands_WithNoCommands_ReturnsEmpty()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();

            // Act
            var commands = buffer.GetCommands<TestCommand>();

            // Assert
            Assert.AreEqual(0, commands.Length);
        }

        [Test]
        public void CommandBuffer_HasCommands_WithCommands_ReturnsTrue()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            buffer.Send(new TestCommand { Value = 1 });

            // Act & Assert
            Assert.IsTrue(buffer.HasCommands<TestCommand>());
        }

        [Test]
        public void CommandBuffer_HasCommands_WithoutCommands_ReturnsFalse()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();

            // Act & Assert
            Assert.IsFalse(buffer.HasCommands<TestCommand>());
        }

        [Test]
        public void CommandBuffer_GetCommandCount_ReturnsCorrectCount()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            buffer.Send(new TestCommand { Value = 1 });
            buffer.Send(new TestCommand { Value = 2 });

            // Act
            var count = buffer.GetCommandCount<TestCommand>();

            // Assert
            Assert.AreEqual(2, count);
        }

        [Test]
        public void CommandBuffer_Clear_RemovesAllCommands()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            buffer.Send(new TestCommand { Value = 1 });
            buffer.Send(new AnotherTestCommand { Data = 2.0f });

            // Act
            buffer.Clear();

            // Assert
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void CommandBuffer_SendDelayed_WithZeroDelay_SendsImmediately()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();

            // Act
            buffer.SendDelayed(new TestCommand { Value = 42 }, 0f);

            // Assert
            Assert.IsTrue(buffer.HasCommands<TestCommand>());
        }

        [Test]
        public void CommandBuffer_SendDelayed_WithDelay_DoesNotExecuteImmediately()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            buffer.SetTime(0f);

            // Act
            buffer.SendDelayed(new TestCommand { Value = 42 }, 1.0f);

            // Assert - command not yet available
            var commands = buffer.GetCommands<TestCommand>();
            Assert.AreEqual(0, commands.Length);
        }

        [Test]
        public void CommandBuffer_SendDelayed_AfterDelay_ExecutesCommand()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            buffer.SetTime(0f);
            buffer.SendDelayed(new TestCommand { Value = 42 }, 0.5f);

            // Act
            buffer.SetTime(0.6f); // Advance past delay
            var commands = buffer.GetCommands<TestCommand>();

            // Assert
            Assert.AreEqual(1, commands.Length);
            Assert.AreEqual(42, commands[0].Value);
        }

        [Test]
        public void CommandBuffer_Update_AdvancesTime()
        {
            // Arrange
            var buffer = new StradaCommandBuffer();
            buffer.SetTime(0f);
            buffer.SendDelayed(new TestCommand { Value = 42 }, 0.5f);

            // Act
            buffer.Update(0.3f);
            var commands1 = buffer.GetCommands<TestCommand>();
            buffer.Update(0.3f);
            var commands2 = buffer.GetCommands<TestCommand>();

            // Assert
            Assert.AreEqual(0, commands1.Length); // Not ready yet
            Assert.AreEqual(1, commands2.Length); // Now ready
        }

        #endregion

        #region Event Bus Tests

        [Test]
        public void EventBus_Raise_AddsEvent()
        {
            // Arrange
            var bus = new StradaEventBus();

            // Act
            bus.Raise(new TestEvent { Value = 42 });

            // Assert
            Assert.AreEqual(1, bus.PendingCount);
        }

        [Test]
        public void EventBus_Subscribe_AddsSubscriber()
        {
            // Arrange
            var bus = new StradaEventBus();
            void Handler(TestEvent evt) { }

            // Act
            bus.Subscribe<TestEvent>(Handler);

            // Assert
            Assert.IsTrue(bus.HasSubscribers<TestEvent>());
            Assert.AreEqual(1, bus.GetSubscriberCount<TestEvent>());
        }

        [Test]
        public void EventBus_Subscribe_SameHandlerTwice_AddsOnce()
        {
            // Arrange
            var bus = new StradaEventBus();
            void Handler(TestEvent evt) { }

            // Act
            bus.Subscribe<TestEvent>(Handler);
            bus.Subscribe<TestEvent>(Handler);

            // Assert
            Assert.AreEqual(1, bus.GetSubscriberCount<TestEvent>());
        }

        [Test]
        public void EventBus_Unsubscribe_RemovesSubscriber()
        {
            // Arrange
            var bus = new StradaEventBus();
            void Handler(TestEvent evt) { }
            bus.Subscribe<TestEvent>(Handler);

            // Act
            bus.Unsubscribe<TestEvent>(Handler);

            // Assert
            Assert.IsFalse(bus.HasSubscribers<TestEvent>());
        }

        [Test]
        public void EventBus_UnsubscribeAll_RemovesAllSubscribers()
        {
            // Arrange
            var bus = new StradaEventBus();
            void Handler1(TestEvent evt) { }
            void Handler2(TestEvent evt) { }
            bus.Subscribe<TestEvent>(Handler1);
            bus.Subscribe<TestEvent>(Handler2);

            // Act
            bus.UnsubscribeAll<TestEvent>();

            // Assert
            Assert.AreEqual(0, bus.GetSubscriberCount<TestEvent>());
        }

        [Test]
        public void EventBus_DispatchEvents_InvokesHandlers()
        {
            // Arrange
            var bus = new StradaEventBus();
            var receivedValues = new List<int>();
            bus.Subscribe<TestEvent>(evt => receivedValues.Add(evt.Value));

            bus.Raise(new TestEvent { Value = 1 });
            bus.Raise(new TestEvent { Value = 2 });
            bus.Raise(new TestEvent { Value = 3 });

            // Act
            bus.DispatchEvents<TestEvent>();

            // Assert
            Assert.AreEqual(3, receivedValues.Count);
            Assert.Contains(1, receivedValues);
            Assert.Contains(2, receivedValues);
            Assert.Contains(3, receivedValues);
        }

        [Test]
        public void EventBus_DispatchEvents_ClearsPendingEvents()
        {
            // Arrange
            var bus = new StradaEventBus();
            bus.Subscribe<TestEvent>(evt => { });
            bus.Raise(new TestEvent { Value = 1 });

            // Act
            bus.DispatchEvents<TestEvent>();

            // Assert
            Assert.AreEqual(0, bus.PendingCount);
        }

        [Test]
        public void EventBus_DispatchPendingEvents_DispatchesAllTypes()
        {
            // Arrange
            var bus = new StradaEventBus();
            var testEventReceived = false;
            var anotherEventReceived = false;

            bus.Subscribe<TestEvent>(evt => testEventReceived = true);
            bus.Subscribe<AnotherTestEvent>(evt => anotherEventReceived = true);

            bus.Raise(new TestEvent { Value = 1 });
            bus.Raise(new AnotherTestEvent { Data = 2.0f });

            // Act
            bus.DispatchPendingEvents();

            // Assert
            Assert.IsTrue(testEventReceived);
            Assert.IsTrue(anotherEventReceived);
        }

        [Test]
        public void EventBus_DispatchEvents_WithNoSubscribers_ClearsQueue()
        {
            // Arrange
            var bus = new StradaEventBus();
            bus.Raise(new TestEvent { Value = 1 });

            // Act
            bus.DispatchEvents<TestEvent>();

            // Assert
            Assert.AreEqual(0, bus.PendingCount);
        }

        [Test]
        public void EventBus_Clear_RemovesAllPendingEvents()
        {
            // Arrange
            var bus = new StradaEventBus();
            bus.Raise(new TestEvent { Value = 1 });
            bus.Raise(new AnotherTestEvent { Data = 2.0f });

            // Act
            bus.Clear();

            // Assert
            Assert.AreEqual(0, bus.PendingCount);
        }

        [Test]
        public void EventBus_MultipleSubscribers_AllReceiveEvent()
        {
            // Arrange
            var bus = new StradaEventBus();
            var received1 = false;
            var received2 = false;
            var received3 = false;

            bus.Subscribe<TestEvent>(evt => received1 = true);
            bus.Subscribe<TestEvent>(evt => received2 = true);
            bus.Subscribe<TestEvent>(evt => received3 = true);

            bus.Raise(new TestEvent { Value = 42 });

            // Act
            bus.DispatchEvents<TestEvent>();

            // Assert
            Assert.IsTrue(received1);
            Assert.IsTrue(received2);
            Assert.IsTrue(received3);
        }

        #endregion

        #region Global Accessors Tests

        [Test]
        public void StradaCommands_Global_NotNull()
        {
            // Act & Assert
            Assert.IsNotNull(StradaCommands.Global);
        }

        [Test]
        public void StradaCommands_Send_UsesGlobalBuffer()
        {
            // Arrange
            StradaCommands.Clear();

            // Act
            StradaCommands.Send(new TestCommand { Value = 42 });

            // Assert
            Assert.IsTrue(StradaCommands.HasCommands<TestCommand>());
        }

        [Test]
        public void StradaEvents_Global_NotNull()
        {
            // Act & Assert
            Assert.IsNotNull(StradaEvents.Global);
        }

        [Test]
        public void StradaEvents_Raise_UsesGlobalBus()
        {
            // Arrange
            StradaEvents.Clear();

            // Act
            StradaEvents.Raise(new TestEvent { Value = 42 });

            // Assert
            Assert.Greater(StradaEvents.Global.PendingCount, 0);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void Integration_CommandToEvent_FullFlow()
        {
            // Arrange
            var commandBuffer = new StradaCommandBuffer();
            var eventBus = new StradaEventBus();

            var eventReceived = false;
            eventBus.Subscribe<TestEvent>(evt =>
            {
                eventReceived = true;
                Assert.AreEqual(42, evt.Value);
            });

            // Act - Simulate MVCS sending command
            commandBuffer.Send(new TestCommand { Value = 42 });

            // Simulate ECS system processing command and raising event
            var commands = commandBuffer.GetCommands<TestCommand>();
            foreach (var cmd in commands)
            {
                // Process command...
                eventBus.Raise(new TestEvent { Value = cmd.Value });
            }

            // Dispatch events back to MVCS
            eventBus.DispatchPendingEvents();

            // Assert
            Assert.IsTrue(eventReceived);
        }

        #endregion

        #region Test Data

        private struct TestCommand : IStradaCommand
        {
            public int Value;
        }

        private struct AnotherTestCommand : IStradaCommand
        {
            public float Data;
        }

        private struct TestEvent : IStradaEvent
        {
            public int Value;
        }

        private struct AnotherTestEvent : IStradaEvent
        {
            public float Data;
        }

        #endregion
    }
}
