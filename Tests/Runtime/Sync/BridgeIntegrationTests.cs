using System;
using NUnit.Framework;
using Strada.Core.Sync;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.World;
using Strada.Core.Patterns;
using UnityEngine;

namespace Strada.Core.Tests.Tests.Runtime.Sync
{
    /// <summary>
    /// Integration tests verifying the full MVCS-ECS Bridge data flow.
    /// Tests Requirements 1.1, 1.2, 1.3 - Architecture Unification.
    /// </summary>
    [TestFixture]
    public class BridgeIntegrationTests
    {
        private World _world;
        private ContainerBuilder _builder;
        private IContainer _container;
        private EventBus _bus;
        private EntityManager _entities;

        [SetUp]
        public void SetUp()
        {
            _world = new ECSBuilder()
                .WithInitialEntityCapacity(128)
                .Build();

            _bus = _world.EventBus;
            _entities = _world.EntityManager;

            _builder = new ContainerBuilder();
            _builder.RegisterInstance(_world);
            _builder.RegisterInstance(_entities);
            _builder.RegisterInstance(_bus);
            _builder.RegisterInstance<IEventBus>(_bus);
            _container = _builder.Build();

            _world.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
            _world?.Dispose();
        }

        /// <summary>
        /// Test: ECS component change → MessageBus event → Patterns controller → View update
        /// Validates Requirements 1.1, 1.3
        /// </summary>
        [Test]
        public void FullDataFlow_ECSComponentChange_PropagatesThrough_MessageBus_To_PatternsController()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            var controller = new TestHealthController();
            controller.Construct(_container);
            controller.Initialize();

            var oldHealth = new HealthComponent { Current = 100, Max = 100 };
            var newHealth = new HealthComponent { Current = 50, Max = 100 };
            var evt = new ComponentChanged<HealthComponent>(entity, oldHealth, newHealth);
            _bus.Publish(evt);

            Assert.AreEqual(1, controller.ReceivedEventCount);
            Assert.AreEqual(entity, controller.LastReceivedEntity);
            Assert.AreEqual(50f, controller.LastReceivedHealth);
        }

        /// <summary>
        /// Test: EntityMediator binds to entity and syncs component data to view
        /// Validates Requirements 1.1
        /// </summary>
        [Test]
        public void EntityMediator_BindsToEntity_SyncsComponentData_ToView()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            var go = new GameObject("TestHealthView");
            var view = go.AddComponent<TestHealthView>();

            var mediator = new TestHealthMediator();
            mediator.Initialize(_container);
            mediator.Bind(entity, view);

            _entities.SetComponent(entity, new HealthComponent { Current = 75, Max = 100 });
            mediator.SyncBindings();

            Assert.AreEqual(75f, view.DisplayedHealth);

            mediator.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Test: ComponentChanged event from ECS triggers EntityMediator handler for bound entity only
        /// Validates Requirements 1.3, 10.3
        /// </summary>
        [Test]
        public void EntityMediator_OnComponentChanged_FiltersForBoundEntityOnly()
        {
            var entity1 = _entities.CreateEntity();
            var entity2 = _entities.CreateEntity();
            _entities.AddComponent(entity1, new HealthComponent { Current = 100, Max = 100 });
            _entities.AddComponent(entity2, new HealthComponent { Current = 80, Max = 100 });

            var go = new GameObject("TestHealthView");
            var view = go.AddComponent<TestHealthView>();

            var mediator = new TestHealthMediatorWithEventHandler();
            mediator.Initialize(_container);
            mediator.Bind(entity1, view);

            _bus.Publish(new ComponentChanged<HealthComponent>(
                entity1,
                new HealthComponent { Current = 100, Max = 100 },
                new HealthComponent { Current = 50, Max = 100 }));

            _bus.Publish(new ComponentChanged<HealthComponent>(
                entity2,
                new HealthComponent { Current = 80, Max = 100 },
                new HealthComponent { Current = 30, Max = 100 }));

            Assert.AreEqual(1, mediator.EventsReceived);
            Assert.AreEqual(50f, mediator.LastHealthValue);

            mediator.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Test: Full round-trip - ECS change → Event → Controller → View update
        /// Validates Requirements 1.1, 1.2, 1.3
        /// </summary>
        [Test]
        public void FullRoundTrip_ECSChange_Event_Controller_ViewUpdate()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            var go = new GameObject("TestHealthView");
            var view = go.AddComponent<TestHealthView>();

            var controller = new TestHealthControllerWithView(view);
            controller.Construct(_container);
            controller.Initialize();

            var evt = new ComponentChanged<HealthComponent>(
                entity,
                new HealthComponent { Current = 100, Max = 100 },
                new HealthComponent { Current = 25, Max = 100 });
            _bus.Publish(evt);

            Assert.AreEqual(25f, view.DisplayedHealth);

            controller.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Test: Patterns controller sends command via MessageBus → ECS system receives and processes
        /// Validates Requirements 1.2
        /// </summary>
        [Test]
        public void CommandFlow_PatternsController_SendsCommand_ECSSystemReceives()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            DamageSignal? receivedCommand = null;
            _bus.RegisterSignalHandler<DamageSignal>(cmd => receivedCommand = cmd);

            var controller = new TestDamageController();
            controller.Construct(_container);
            controller.Initialize();

            controller.SendDamageCommand(entity, 30f);

            Assert.IsNotNull(receivedCommand);
            Assert.AreEqual(entity, receivedCommand.Value.Target);
            Assert.AreEqual(30f, receivedCommand.Value.Amount);

            controller.Dispose();
        }

        /// <summary>
        /// Test: Command from MVCS modifies ECS component state
        /// Validates Requirements 1.2
        /// </summary>
        [Test]
        public void CommandFlow_Command_ModifiesECSComponentState()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            _bus.RegisterSignalHandler<DamageSignal>(cmd =>
            {
                if (_entities.HasComponent<HealthComponent>(cmd.Target))
                {
                    var health = _entities.GetComponent<HealthComponent>(cmd.Target);
                    health.Current -= cmd.Amount;
                    _entities.SetComponent(cmd.Target, health);
                }
            });

            var controller = new TestDamageController();
            controller.Construct(_container);
            controller.Initialize();

            controller.SendDamageCommand(entity, 40f);

            var updatedHealth = _entities.GetComponent<HealthComponent>(entity);
            Assert.AreEqual(60f, updatedHealth.Current);

            controller.Dispose();
        }

        /// <summary>
        /// Test: Bidirectional flow - Command modifies ECS → Event published → Controller notified
        /// Validates Requirements 1.2, 1.3
        /// </summary>
        [Test]
        public void BidirectionalFlow_Command_ModifiesECS_EventNotifiesController()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            _bus.RegisterSignalHandler<DamageSignal>(cmd =>
            {
                if (_entities.HasComponent<HealthComponent>(cmd.Target))
                {
                    var oldHealth = _entities.GetComponent<HealthComponent>(cmd.Target);
                    var newHealth = oldHealth;
                    newHealth.Current -= cmd.Amount;
                    _entities.SetComponent(cmd.Target, newHealth);

                    _bus.Publish(new ComponentChanged<HealthComponent>(cmd.Target, oldHealth, newHealth));
                }
            });

            var controller = new TestHealthController();
            controller.Construct(_container);
            controller.Initialize();

            var damageController = new TestDamageController();
            damageController.Construct(_container);
            damageController.Initialize();

            damageController.SendDamageCommand(entity, 35f);

            Assert.AreEqual(1, controller.ReceivedEventCount);
            Assert.AreEqual(65f, controller.LastReceivedHealth);

            controller.Dispose();
            damageController.Dispose();
        }

        /// <summary>
        /// Test: Multiple commands in sequence maintain state consistency
        /// Validates Requirements 1.2
        /// </summary>
        [Test]
        public void CommandFlow_MultipleCommands_MaintainStateConsistency()
        {
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            _bus.RegisterSignalHandler<DamageSignal>(cmd =>
            {
                var health = _entities.GetComponent<HealthComponent>(cmd.Target);
                health.Current = Math.Max(0, health.Current - cmd.Amount);
                _entities.SetComponent(cmd.Target, health);
            });

            _bus.RegisterSignalHandler<HealSignal>(cmd =>
            {
                var health = _entities.GetComponent<HealthComponent>(cmd.Target);
                health.Current = Math.Min(health.Max, health.Current + cmd.Amount);
                _entities.SetComponent(cmd.Target, health);
            });

            var controller = new TestCombatController();
            controller.Construct(_container);
            controller.Initialize();

            controller.SendDamage(entity, 30f);
            controller.SendHeal(entity, 15f);
            controller.SendDamage(entity, 50f);
            controller.SendHeal(entity, 100f);

            var finalHealth = _entities.GetComponent<HealthComponent>(entity);
            Assert.AreEqual(100f, finalHealth.Current);

            controller.Dispose();
        }

        private struct HealthComponent : IComponent
        {
            public float Current;
            public float Max;
        }

        private struct DamageSignal
        {
            public Entity Target;
            public float Amount;
        }

        private struct HealSignal
        {
            public Entity Target;
            public float Amount;
        }

        private class TestHealthView : View
        {
            public float DisplayedHealth { get; private set; }

            public void UpdateHealth(float health)
            {
                DisplayedHealth = health;
            }
        }

        private class TestHealthController : Controller
        {
            public int ReceivedEventCount { get; private set; }
            public Entity LastReceivedEntity { get; private set; }
            public float LastReceivedHealth { get; private set; }

            protected override void OnInitialize()
            {
                Subscribe<ComponentChanged<HealthComponent>>(OnHealthChanged);
            }

            private void OnHealthChanged(ComponentChanged<HealthComponent> evt)
            {
                ReceivedEventCount++;
                LastReceivedEntity = evt.Entity;
                LastReceivedHealth = evt.NewValue.Current;
            }
        }

        private class TestHealthControllerWithView : Controller
        {
            private readonly TestHealthView _view;

            public TestHealthControllerWithView(TestHealthView view)
            {
                _view = view;
            }

            protected override void OnInitialize()
            {
                Subscribe<ComponentChanged<HealthComponent>>(OnHealthChanged);
            }

            private void OnHealthChanged(ComponentChanged<HealthComponent> evt)
            {
                _view.UpdateHealth(evt.NewValue.Current);
            }
        }

        private class TestDamageController : Controller
        {
            public void SendDamageCommand(Entity target, float amount)
            {
                Send(new DamageSignal { Target = target, Amount = amount });
            }
        }

        private class TestCombatController : Controller
        {
            public void SendDamage(Entity target, float amount)
            {
                Send(new DamageSignal { Target = target, Amount = amount });
            }

            public void SendHeal(Entity target, float amount)
            {
                Send(new HealSignal { Target = target, Amount = amount });
            }
        }

        private class TestHealthMediator : EntityMediator<TestHealthView>
        {
            protected override void OnBind()
            {
                Bind<HealthComponent, float>(
                    c => c.Current,
                    v => View.UpdateHealth(v));
            }

            protected override void OnUnbind() { }
        }

        private class TestHealthMediatorWithEventHandler : EntityMediator<TestHealthView>
        {
            public int EventsReceived { get; private set; }
            public float LastHealthValue { get; private set; }

            protected override void OnBind()
            {
                OnComponentChanged<HealthComponent>(OnHealthChanged);
            }

            private void OnHealthChanged(ComponentChanged<HealthComponent> evt)
            {
                EventsReceived++;
                LastHealthValue = evt.NewValue.Current;
            }

            protected override void OnUnbind() { }
        }
    }
}
