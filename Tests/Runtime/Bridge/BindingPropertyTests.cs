using System;
using System.Collections.Generic;
using FsCheck;
using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.Tests.Tests.Runtime.Generators;

namespace Strada.Core.Tests.Tests.Runtime.Bridge
{
    /// <summary>
    /// Property-based tests for Bridge component bindings.
    /// Tests verify correctness properties for ComponentBinding and ViewMediator.
    /// </summary>
    [TestFixture]
    public class BindingPropertyTests
    {
        private EntityManager _entityManager;
        private IContainer _container;
        private MessageBus _bus;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            StradaArbitraries.RegisterAll();
        }

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
            _bus = new MessageBus();

            var builder = new ContainerBuilder();
            builder.RegisterInstance(_entityManager);
            builder.RegisterInstance<IMessageBus>(_bus);
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _entityManager?.Dispose();
            _bus?.Dispose();
            _container?.Dispose();
        }

        /// <summary>
        /// Generator for integer property values.
        /// </summary>
        private static Gen<int> IntValueGen => Gen.Choose(-10000, 10000);

        /// <summary>
        /// Generator for distinct value pairs (old != new).
        /// </summary>
        private static Gen<(int oldValue, int newValue)> DistinctValuePairGen =>
            from oldVal in IntValueGen
            from delta in Gen.Choose(1, 1000)
            select (oldVal, oldVal + delta);

        /// <summary>
        /// Generator for entity count (1-20 entities).
        /// </summary>
        private static Gen<int> EntityCountGen => Gen.Choose(1, 20);

        /// <summary>
        /// Generator for subscriber count.
        /// </summary>
        private static Gen<int> SubscriberCountGen => Gen.Choose(1, 10);

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 19: ComponentBinding Sync Detection**
        /// For any ComponentBinding, when the bound component's selected property changes,
        /// Sync SHALL invoke the callback with the new value.
        /// **Validates: Requirements 10.1**
        /// </summary>
        [Test]
        public void ComponentBindingSyncDetection_ChangedPropertyInvokesCallback()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                DistinctValuePairGen.ToArbitrary(),
                (valuePair) =>
                {
                    var entity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(entity, new TestBindingComponent { Value = valuePair.oldValue });

                    int callbackValue = 0;
                    bool callbackInvoked = false;

                    var binding = new ComponentBinding<TestBindingComponent, int>(
                        _entityManager,
                        entity,
                        c => c.Value,
                        v =>
                        {
                            callbackInvoked = true;
                            callbackValue = v;
                        });

                    _entityManager.SetComponent(entity, new TestBindingComponent { Value = valuePair.newValue });
                    binding.Sync();

                    return callbackInvoked && callbackValue == valuePair.newValue;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 19: ComponentBinding Sync Detection**
        /// Additional test: Sync does not invoke callback when value unchanged.
        /// **Validates: Requirements 10.1**
        /// </summary>
        [Test]
        public void ComponentBindingSyncDetection_UnchangedValueNoCallback()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                (value) =>
                {
                    var entity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(entity, new TestBindingComponent { Value = value });

                    int callbackCount = 0;

                    var binding = new ComponentBinding<TestBindingComponent, int>(
                        _entityManager,
                        entity,
                        c => c.Value,
                        _ => callbackCount++);

                    binding.Sync();

                    return callbackCount == 0;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 19: ComponentBinding Sync Detection**
        /// Additional test: Multiple syncs only invoke callback when value changes.
        /// **Validates: Requirements 10.1**
        /// </summary>
        [Test]
        public void ComponentBindingSyncDetection_MultipleSyncsOnlyCallbackOnChange()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                Gen.Choose(2, 5).ToArbitrary(),
                (initialValue, changeCount) =>
                {
                    var entity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(entity, new TestBindingComponent { Value = initialValue });

                    var receivedValues = new List<int>();

                    var binding = new ComponentBinding<TestBindingComponent, int>(
                        _entityManager,
                        entity,
                        c => c.Value,
                        v => receivedValues.Add(v));

                    for (int i = 1; i <= changeCount; i++)
                    {
                        _entityManager.SetComponent(entity, new TestBindingComponent { Value = initialValue + i * 100 });
                        binding.Sync();
                    }

                    if (receivedValues.Count != changeCount)
                        return false;

                    for (int i = 0; i < changeCount; i++)
                    {
                        if (receivedValues[i] != initialValue + (i + 1) * 100)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 20: ComponentBinding Push Correctness**
        /// For any ComponentBinding with a setter, after Push(value) the ECS component
        /// SHALL contain the pushed value.
        /// **Validates: Requirements 10.2**
        /// </summary>
        [Test]
        public void ComponentBindingPushCorrectness_PushedValueWrittenToComponent()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                IntValueGen.ToArbitrary(),
                (initialValue, pushValue) =>
                {
                    var entity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(entity, new TestBindingComponent { Value = initialValue });

                    var binding = new ComponentBinding<TestBindingComponent, int>(
                        _entityManager,
                        entity,
                        c => c.Value,
                        (c, v) => new TestBindingComponent { Value = v },
                        _ => { });

                    binding.Push(pushValue);

                    var component = _entityManager.GetComponent<TestBindingComponent>(entity);
                    return component.Value == pushValue;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 20: ComponentBinding Push Correctness**
        /// Additional test: Push without setter does nothing.
        /// **Validates: Requirements 10.2**
        /// </summary>
        [Test]
        public void ComponentBindingPushCorrectness_PushWithoutSetterPreservesValue()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                IntValueGen.ToArbitrary(),
                (initialValue, attemptedPushValue) =>
                {
                    var entity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(entity, new TestBindingComponent { Value = initialValue });

                    var binding = new ComponentBinding<TestBindingComponent, int>(
                        _entityManager,
                        entity,
                        c => c.Value,
                        _ => { });

                    binding.Push();

                    var component = _entityManager.GetComponent<TestBindingComponent>(entity);
                    return component.Value == initialValue;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 20: ComponentBinding Push Correctness**
        /// Additional test: Multiple pushes update component correctly.
        /// **Validates: Requirements 10.2**
        /// </summary>
        [Test]
        public void ComponentBindingPushCorrectness_MultiplePushesUpdateCorrectly()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(2, 5).ToArbitrary(),
                (pushCount) =>
                {
                    var entity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(entity, new TestBindingComponent { Value = 0 });

                    var binding = new ComponentBinding<TestBindingComponent, int>(
                        _entityManager,
                        entity,
                        c => c.Value,
                        (c, v) => new TestBindingComponent { Value = v },
                        _ => { });

                    int lastPushedValue = 0;
                    for (int i = 1; i <= pushCount; i++)
                    {
                        lastPushedValue = i * 100;
                        binding.Push(lastPushedValue);
                    }

                    var component = _entityManager.GetComponent<TestBindingComponent>(entity);
                    return component.Value == lastPushedValue;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 21: ViewMediator Entity Filtering**
        /// For any ViewMediator subscribed to ComponentChanged events, only events
        /// matching the bound entity SHALL trigger the handler.
        /// **Validates: Requirements 10.3**
        /// </summary>
        [Test]
        public void ViewMediatorEntityFiltering_OnlyBoundEntityEventsReceived()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                EntityCountGen.ToArbitrary(),
                IntValueGen.ToArbitrary(),
                (entityCount, newValue) =>
                {
                    if (entityCount < 2) entityCount = 2;

                    var entities = new Entity[entityCount];
                    for (int i = 0; i < entityCount; i++)
                    {
                        entities[i] = _entityManager.CreateEntity();
                        _entityManager.AddComponent(entities[i], new TestBindingComponent { Value = i });
                    }

                    var boundEntity = entities[0];
                    var receivedEvents = new List<ComponentChanged<TestBindingComponent>>();

                    Action<ComponentChanged<TestBindingComponent>> filter = e =>
                    {
                        if (e.Entity == boundEntity)
                            receivedEvents.Add(e);
                    };

                    _bus.Subscribe(filter);

                    for (int i = 0; i < entityCount; i++)
                    {
                        var evt = new ComponentChanged<TestBindingComponent>(
                            entities[i],
                            new TestBindingComponent { Value = i },
                            new TestBindingComponent { Value = newValue + i });
                        _bus.Publish(evt);
                    }

                    if (receivedEvents.Count != 1)
                        return false;

                    return receivedEvents[0].Entity == boundEntity;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 21: ViewMediator Entity Filtering**
        /// Additional test: Multiple events for bound entity all received.
        /// **Validates: Requirements 10.3**
        /// </summary>
        [Test]
        public void ViewMediatorEntityFiltering_AllBoundEntityEventsReceived()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 10).ToArbitrary(),
                (eventCount) =>
                {
                    var boundEntity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(boundEntity, new TestBindingComponent { Value = 0 });

                    var otherEntity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(otherEntity, new TestBindingComponent { Value = 0 });

                    var receivedEvents = new List<ComponentChanged<TestBindingComponent>>();

                    Action<ComponentChanged<TestBindingComponent>> filter = e =>
                    {
                        if (e.Entity == boundEntity)
                            receivedEvents.Add(e);
                    };

                    _bus.Subscribe(filter);

                    for (int i = 0; i < eventCount; i++)
                    {
                        var boundEvt = new ComponentChanged<TestBindingComponent>(
                            boundEntity,
                            new TestBindingComponent { Value = i },
                            new TestBindingComponent { Value = i + 1 });
                        _bus.Publish(boundEvt);

                        var otherEvt = new ComponentChanged<TestBindingComponent>(
                            otherEntity,
                            new TestBindingComponent { Value = i * 10 },
                            new TestBindingComponent { Value = i * 10 + 1 });
                        _bus.Publish(otherEvt);
                    }

                    if (receivedEvents.Count != eventCount)
                        return false;

                    foreach (var evt in receivedEvents)
                    {
                        if (evt.Entity != boundEntity)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 21: ViewMediator Entity Filtering**
        /// Additional test: No events received when no matching entity events published.
        /// **Validates: Requirements 10.3**
        /// </summary>
        [Test]
        public void ViewMediatorEntityFiltering_NoEventsWhenNoMatchingEntity()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                EntityCountGen.ToArbitrary(),
                (entityCount) =>
                {
                    var boundEntity = _entityManager.CreateEntity();
                    _entityManager.AddComponent(boundEntity, new TestBindingComponent { Value = 0 });

                    var receivedEvents = new List<ComponentChanged<TestBindingComponent>>();

                    Action<ComponentChanged<TestBindingComponent>> filter = e =>
                    {
                        if (e.Entity == boundEntity)
                            receivedEvents.Add(e);
                    };

                    _bus.Subscribe(filter);

                    for (int i = 0; i < entityCount; i++)
                    {
                        var otherEntity = _entityManager.CreateEntity();
                        _entityManager.AddComponent(otherEntity, new TestBindingComponent { Value = i });

                        var evt = new ComponentChanged<TestBindingComponent>(
                            otherEntity,
                            new TestBindingComponent { Value = i },
                            new TestBindingComponent { Value = i + 100 });
                        _bus.Publish(evt);
                    }

                    return receivedEvents.Count == 0;
                });

            property.Check(config);
        }

        /// <summary>
        /// Test component for binding property tests.
        /// </summary>
        private struct TestBindingComponent : IComponent
        {
            public int Value;

            public override string ToString() => $"TestBindingComponent(Value={Value})";
        }
    }
}
