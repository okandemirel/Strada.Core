using System;
using System.Collections.Generic;
using FsCheck;
using NUnit.Framework;
using Strada.Core.Communication;
using Strada.Core.Tests.Runtime.Generators;

namespace Strada.Core.Tests.Runtime.Communication
{
    /// <summary>
    /// Property-based tests for StradaBus messaging system.
    /// Tests verify correctness properties that must hold across all valid inputs.
    /// </summary>
    [TestFixture]
    public class BusPropertyTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            StradaArbitraries.RegisterAll();
        }

        #region Generators

        /// <summary>
        /// Generator for subscriber count (1-20).
        /// </summary>
        private static Gen<int> SubscriberCountGen => Gen.Choose(1, 20);

        /// <summary>
        /// Generator for event value.
        /// </summary>
        private static Gen<int> EventValueGen => Gen.Choose(1, 10000);

        /// <summary>
        /// Generator for command value.
        /// </summary>
        private static Gen<int> CommandValueGen => Gen.Choose(1, 10000);

        /// <summary>
        /// Generator for query multiplier.
        /// </summary>
        private static Gen<int> QueryMultiplierGen => Gen.Choose(1, 100);

        /// <summary>
        /// Generator for publish count (1-10).
        /// </summary>
        private static Gen<int> PublishCountGen => Gen.Choose(1, 10);

        #endregion

        #region Property 10: Event Delivery Completeness

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 10: Event Delivery Completeness**
        /// For any event published to StradaBus with N subscribers,
        /// all N subscribers SHALL receive the event exactly once.
        /// **Validates: Requirements 4.1**
        /// </summary>
        [Test]
        public void EventDeliveryCompleteness_AllSubscribersReceiveEvent()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                SubscriberCountGen.ToArbitrary(),
                EventValueGen.ToArbitrary(),
                (subscriberCount, eventValue) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    var receivedValues = new List<int>();
                    var receiveCounts = new int[subscriberCount];

                    // Subscribe N handlers
                    for (int i = 0; i < subscriberCount; i++)
                    {
                        int index = i;
                        bus.Subscribe<TestEvent>(e =>
                        {
                            receivedValues.Add(e.Value);
                            receiveCounts[index]++;
                        });
                    }

                    // Act - publish event
                    bus.Publish(new TestEvent { Value = eventValue });

                    // Assert - all subscribers received exactly once with correct value
                    if (receivedValues.Count != subscriberCount)
                        return false;

                    foreach (var value in receivedValues)
                    {
                        if (value != eventValue)
                            return false;
                    }

                    foreach (var count in receiveCounts)
                    {
                        if (count != 1)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 10: Event Delivery Completeness**
        /// Additional test: Multiple events are all delivered to all subscribers.
        /// **Validates: Requirements 4.1**
        /// </summary>
        [Test]
        public void EventDeliveryCompleteness_MultipleEventsAllDelivered()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                SubscriberCountGen.ToArbitrary(),
                PublishCountGen.ToArbitrary(),
                (subscriberCount, publishCount) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    var totalReceived = new int[subscriberCount];

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        int index = i;
                        bus.Subscribe<TestEvent>(_ => totalReceived[index]++);
                    }

                    // Act - publish multiple events
                    for (int p = 0; p < publishCount; p++)
                    {
                        bus.Publish(new TestEvent { Value = p });
                    }

                    // Assert - each subscriber received all events
                    foreach (var count in totalReceived)
                    {
                        if (count != publishCount)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        #endregion

        #region Property 11: Command Handler Invocation

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 11: Command Handler Invocation**
        /// For any command sent to StradaBus with a registered handler,
        /// the handler SHALL be invoked exactly once with the command data.
        /// **Validates: Requirements 4.2**
        /// </summary>
        [Test]
        public void CommandHandlerInvocation_HandlerInvokedExactlyOnce()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                CommandValueGen.ToArbitrary(),
                (commandValue) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    int invokeCount = 0;
                    int receivedValue = 0;

                    bus.RegisterCommandHandler<TestCommand>(cmd =>
                    {
                        invokeCount++;
                        receivedValue = cmd.Value;
                    });

                    // Act
                    bus.Send(new TestCommand { Value = commandValue });

                    // Assert - handler invoked exactly once with correct data
                    return invokeCount == 1 && receivedValue == commandValue;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 11: Command Handler Invocation**
        /// Additional test: Multiple sends invoke handler multiple times.
        /// **Validates: Requirements 4.2**
        /// </summary>
        [Test]
        public void CommandHandlerInvocation_MultipleSendsInvokeMultipleTimes()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                PublishCountGen.ToArbitrary(),
                (sendCount) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    int invokeCount = 0;
                    var receivedValues = new List<int>();

                    bus.RegisterCommandHandler<TestCommand>(cmd =>
                    {
                        invokeCount++;
                        receivedValues.Add(cmd.Value);
                    });

                    // Act - send multiple commands
                    for (int i = 0; i < sendCount; i++)
                    {
                        bus.Send(new TestCommand { Value = i + 1 });
                    }

                    // Assert - handler invoked once per send
                    if (invokeCount != sendCount)
                        return false;

                    // Verify all values received in order
                    for (int i = 0; i < sendCount; i++)
                    {
                        if (receivedValues[i] != i + 1)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        #endregion

        #region Property 12: Query Result Correctness

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 12: Query Result Correctness**
        /// For any query sent to StradaBus, the returned result SHALL equal
        /// the value returned by the registered handler.
        /// **Validates: Requirements 4.3**
        /// </summary>
        [Test]
        public void QueryResultCorrectness_ReturnsHandlerResult()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                QueryMultiplierGen.ToArbitrary(),
                (multiplier) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    const int baseValue = 10;
                    int expectedResult = baseValue * multiplier;

                    bus.RegisterQueryHandler<TestQuery, int>(q => baseValue * q.Multiplier);

                    // Act
                    var result = bus.Query<TestQuery, int>(new TestQuery { Multiplier = multiplier });

                    // Assert
                    return result == expectedResult;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 12: Query Result Correctness**
        /// Additional test: Query with string result returns correct value.
        /// **Validates: Requirements 4.3**
        /// </summary>
        [Test]
        public void QueryResultCorrectness_StringResult_ReturnsCorrectValue()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 1000).ToArbitrary(),
                (id) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    string expectedResult = $"Result_{id}";

                    bus.RegisterQueryHandler<TestStringQuery, string>(q => $"Result_{q.Id}");

                    // Act
                    var result = bus.Query<TestStringQuery, string>(new TestStringQuery { Id = id });

                    // Assert
                    return result == expectedResult;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 12: Query Result Correctness**
        /// Additional test: Multiple queries return consistent results.
        /// **Validates: Requirements 4.3**
        /// </summary>
        [Test]
        public void QueryResultCorrectness_MultipleQueries_ConsistentResults()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                QueryMultiplierGen.ToArbitrary(),
                PublishCountGen.ToArbitrary(),
                (multiplier, queryCount) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    bus.RegisterQueryHandler<TestQuery, int>(q => 10 * q.Multiplier);

                    // Act - query multiple times
                    var results = new List<int>();
                    for (int i = 0; i < queryCount; i++)
                    {
                        results.Add(bus.Query<TestQuery, int>(new TestQuery { Multiplier = multiplier }));
                    }

                    // Assert - all results should be the same
                    int expected = 10 * multiplier;
                    foreach (var result in results)
                    {
                        if (result != expected)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        #endregion

        #region Property 13: Unsubscribe Effectiveness

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 13: Unsubscribe Effectiveness**
        /// For any handler that has been unsubscribed, subsequent event publications
        /// SHALL NOT invoke that handler.
        /// **Validates: Requirements 4.4**
        /// </summary>
        [Test]
        public void UnsubscribeEffectiveness_UnsubscribedHandlerNotInvoked()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                PublishCountGen.ToArbitrary(),
                (publishCount) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    int invokeCount = 0;
                    Action<TestEvent> handler = _ => invokeCount++;

                    bus.Subscribe(handler);

                    // Publish once before unsubscribe
                    bus.Publish(new TestEvent { Value = 1 });
                    int countBeforeUnsubscribe = invokeCount;

                    // Unsubscribe
                    bus.Unsubscribe(handler);

                    // Act - publish multiple times after unsubscribe
                    for (int i = 0; i < publishCount; i++)
                    {
                        bus.Publish(new TestEvent { Value = i + 2 });
                    }

                    // Assert - count should not have changed after unsubscribe
                    return countBeforeUnsubscribe == 1 && invokeCount == 1;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 13: Unsubscribe Effectiveness**
        /// Additional test: Unsubscribing one handler doesn't affect others.
        /// **Validates: Requirements 4.4**
        /// </summary>
        [Test]
        public void UnsubscribeEffectiveness_OtherHandlersStillReceive()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                SubscriberCountGen.ToArbitrary(),
                PublishCountGen.ToArbitrary(),
                (subscriberCount, publishCount) =>
                {
                    if (subscriberCount < 2) subscriberCount = 2; // Need at least 2 subscribers

                    // Arrange
                    using var bus = new StradaBus();
                    var invokeCounts = new int[subscriberCount];
                    var handlers = new Action<TestEvent>[subscriberCount];

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        int index = i;
                        handlers[i] = _ => invokeCounts[index]++;
                        bus.Subscribe(handlers[i]);
                    }

                    // Unsubscribe the first handler
                    bus.Unsubscribe(handlers[0]);

                    // Act - publish events
                    for (int p = 0; p < publishCount; p++)
                    {
                        bus.Publish(new TestEvent { Value = p });
                    }

                    // Assert - first handler should have 0 invocations
                    if (invokeCounts[0] != 0)
                        return false;

                    // All other handlers should have received all events
                    for (int i = 1; i < subscriberCount; i++)
                    {
                        if (invokeCounts[i] != publishCount)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 13: Unsubscribe Effectiveness**
        /// Additional test: Subscriber count decreases after unsubscribe.
        /// **Validates: Requirements 4.4**
        /// </summary>
        [Test]
        public void UnsubscribeEffectiveness_SubscriberCountDecreases()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                SubscriberCountGen.ToArbitrary(),
                (subscriberCount) =>
                {
                    // Arrange
                    using var bus = new StradaBus();
                    var handlers = new Action<TestEvent>[subscriberCount];

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        handlers[i] = _ => { };
                        bus.Subscribe(handlers[i]);
                    }

                    int countBefore = bus.GetSubscriberCount<TestEvent>();

                    // Act - unsubscribe first handler
                    bus.Unsubscribe(handlers[0]);

                    int countAfter = bus.GetSubscriberCount<TestEvent>();

                    // Assert
                    return countBefore == subscriberCount && countAfter == subscriberCount - 1;
                });

            property.Check(config);
        }

        #endregion

        #region Test Types

        private struct TestEvent
        {
            public int Value;
        }

        private struct TestCommand
        {
            public int Value;
        }

        private struct TestQuery : IQuery<int>
        {
            public int Multiplier;
        }

        private struct TestStringQuery : IQuery<string>
        {
            public int Id;
        }

        #endregion
    }
}
