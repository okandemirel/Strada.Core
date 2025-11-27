using System;
using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.Communication;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Systems;
using Strada.Core.MVCS;
using UnityEngine;

namespace Strada.Core.Tests.Bridge
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
        private StradaBus _bus;
        private EntityManager _entities;

        [SetUp]
        public void SetUp()
        {
            // Build ECS World with systems
            _world = new WorldBuilder()
                .WithInitialEntityCapacity(128)
                .Build();
            
            _bus = _world.Bus;
            _entities = _world.Entities;
            
            // Build DI Container with ECS components registered
            _builder = new ContainerBuilder();
            _builder.RegisterInstance(_world);
            _builder.RegisterInstance(_entities);
            _builder.RegisterInstance(_bus);
            _builder.RegisterInstance<IStradaBus>(_bus);
            _container = _builder.Build();
            
            _world.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
            _world?.Dispose();
        }

        #region Task 21.1: Full Data Flow Tests (ECS → StradaBus → MVCS Controller → View)

        /// <summary>
        /// Test: ECS component change → StradaBus event → MVCS controller → View update
        /// Validates Requirements 1.1, 1.3
        /// </summary>
        [Test]
        public void FullDataFlow_ECSComponentChange_PropagatesThrough_StradaBus_To_MVCSController()
        {
            // Arrange: Create entity with component
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            // Create and initialize controller
            var controller = new TestHealthController();
            controller.Construct(_container);
            controller.Initialize();

            // Act: Publish ComponentChanged event (simulating ECS system publishing)
            var oldHealth = new HealthComponent { Current = 100, Max = 100 };
            var newHealth = new HealthComponent { Current = 50, Max = 100 };
            var evt = new ComponentChanged<HealthComponent>(entity, oldHealth, newHealth);
            _bus.Publish(evt);

            // Assert: Controller received the event
            Assert.AreEqual(1, controller.ReceivedEventCount);
            Assert.AreEqual(entity, controller.LastReceivedEntity);
            Assert.AreEqual(50f, controller.LastReceivedHealth);
        }

        /// <summary>
        /// Test: ViewMediator binds to entity and syncs component data to view
        /// Validates Requirements 1.1
        /// </summary>
        [Test]
        public void ViewMediator_BindsToEntity_SyncsComponentData_ToView()
        {
            // Arrange: Create entity with component
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            // Create view and mediator
            var go = new GameObject("TestHealthView");
            var view = go.AddComponent<TestHealthView>();
            
            var mediator = new TestHealthMediator();
            mediator.Initialize(_container);
            mediator.Bind(entity, view);

            // Act: Change component and sync
            _entities.SetComponent(entity, new HealthComponent { Current = 75, Max = 100 });
            mediator.SyncBindings();

            // Assert: View received the updated value
            Assert.AreEqual(75f, view.DisplayedHealth);

            // Cleanup
            mediator.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Test: ComponentChanged event from ECS triggers ViewMediator handler for bound entity only
        /// Validates Requirements 1.3, 10.3
        /// </summary>
        [Test]
        public void ViewMediator_OnComponentChanged_FiltersForBoundEntityOnly()
        {
            // Arrange: Create two entities
            var entity1 = _entities.CreateEntity();
            var entity2 = _entities.CreateEntity();
            _entities.AddComponent(entity1, new HealthComponent { Current = 100, Max = 100 });
            _entities.AddComponent(entity2, new HealthComponent { Current = 80, Max = 100 });

            // Create view and mediator bound to entity1
            var go = new GameObject("TestHealthView");
            var view = go.AddComponent<TestHealthView>();
            
            var mediator = new TestHealthMediatorWithEventHandler();
            mediator.Initialize(_container);
            mediator.Bind(entity1, view);

            // Act: Publish events for both entities
            _bus.Publish(new ComponentChanged<HealthComponent>(
                entity1, 
                new HealthComponent { Current = 100, Max = 100 },
                new HealthComponent { Current = 50, Max = 100 }));
            
            _bus.Publish(new ComponentChanged<HealthComponent>(
                entity2, 
                new HealthComponent { Current = 80, Max = 100 },
                new HealthComponent { Current = 30, Max = 100 }));

            // Assert: Only entity1's event was handled
            Assert.AreEqual(1, mediator.EventsReceived);
            Assert.AreEqual(50f, mediator.LastHealthValue);

            // Cleanup
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
            // Arrange
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            var go = new GameObject("TestHealthView");
            var view = go.AddComponent<TestHealthView>();
            
            // Create controller that updates view
            var controller = new TestHealthControllerWithView(view);
            controller.Construct(_container);
            controller.Initialize();

            // Act: Simulate ECS system publishing component change
            var evt = new ComponentChanged<HealthComponent>(
                entity,
                new HealthComponent { Current = 100, Max = 100 },
                new HealthComponent { Current = 25, Max = 100 });
            _bus.Publish(evt);

            // Assert: View was updated by controller
            Assert.AreEqual(25f, view.DisplayedHealth);

            // Cleanup
            controller.Dispose();
            UnityEngine.Object.DestroyImmediate(go);
        }

        #endregion

        #region Task 21.2: Command Flow Tests (MVCS Controller → StradaBus → ECS System)

        /// <summary>
        /// Test: MVCS controller sends command via StradaBus → ECS system receives and processes
        /// Validates Requirements 1.2
        /// </summary>
        [Test]
        public void CommandFlow_MVCSController_SendsCommand_ECSSystemReceives()
        {
            // Arrange: Create entity
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            // Register command handler (simulating ECS system)
            DamageCommand? receivedCommand = null;
            _bus.RegisterCommandHandler<DamageCommand>(cmd => receivedCommand = cmd);

            // Create controller
            var controller = new TestDamageController();
            controller.Construct(_container);
            controller.Initialize();

            // Act: Controller sends command
            controller.SendDamageCommand(entity, 30f);

            // Assert: Command was received
            Assert.IsNotNull(receivedCommand);
            Assert.AreEqual(entity, receivedCommand.Value.Target);
            Assert.AreEqual(30f, receivedCommand.Value.Amount);

            // Cleanup
            controller.Dispose();
        }

        /// <summary>
        /// Test: Command from MVCS modifies ECS component state
        /// Validates Requirements 1.2
        /// </summary>
        [Test]
        public void CommandFlow_Command_ModifiesECSComponentState()
        {
            // Arrange: Create entity with health
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            // Register command handler that modifies component
            _bus.RegisterCommandHandler<DamageCommand>(cmd =>
            {
                if (_entities.HasComponent<HealthComponent>(cmd.Target))
                {
                    var health = _entities.GetComponent<HealthComponent>(cmd.Target);
                    health.Current -= cmd.Amount;
                    _entities.SetComponent(cmd.Target, health);
                }
            });

            // Create controller
            var controller = new TestDamageController();
            controller.Construct(_container);
            controller.Initialize();

            // Act: Controller sends damage command
            controller.SendDamageCommand(entity, 40f);

            // Assert: Component was modified
            var updatedHealth = _entities.GetComponent<HealthComponent>(entity);
            Assert.AreEqual(60f, updatedHealth.Current);

            // Cleanup
            controller.Dispose();
        }

        /// <summary>
        /// Test: Bidirectional flow - Command modifies ECS → Event published → Controller notified
        /// Validates Requirements 1.2, 1.3
        /// </summary>
        [Test]
        public void BidirectionalFlow_Command_ModifiesECS_EventNotifiesController()
        {
            // Arrange
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            // Register command handler that modifies component AND publishes event
            _bus.RegisterCommandHandler<DamageCommand>(cmd =>
            {
                if (_entities.HasComponent<HealthComponent>(cmd.Target))
                {
                    var oldHealth = _entities.GetComponent<HealthComponent>(cmd.Target);
                    var newHealth = oldHealth;
                    newHealth.Current -= cmd.Amount;
                    _entities.SetComponent(cmd.Target, newHealth);
                    
                    // Publish change event
                    _bus.Publish(new ComponentChanged<HealthComponent>(cmd.Target, oldHealth, newHealth));
                }
            });

            // Create controller that listens for health changes
            var controller = new TestHealthController();
            controller.Construct(_container);
            controller.Initialize();

            // Create damage controller
            var damageController = new TestDamageController();
            damageController.Construct(_container);
            damageController.Initialize();

            // Act: Send damage command
            damageController.SendDamageCommand(entity, 35f);

            // Assert: Health controller received the change event
            Assert.AreEqual(1, controller.ReceivedEventCount);
            Assert.AreEqual(65f, controller.LastReceivedHealth);

            // Cleanup
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
            // Arrange
            var entity = _entities.CreateEntity();
            _entities.AddComponent(entity, new HealthComponent { Current = 100, Max = 100 });

            // Register command handlers
            _bus.RegisterCommandHandler<DamageCommand>(cmd =>
            {
                var health = _entities.GetComponent<HealthComponent>(cmd.Target);
                health.Current = Math.Max(0, health.Current - cmd.Amount);
                _entities.SetComponent(cmd.Target, health);
            });

            _bus.RegisterCommandHandler<HealCommand>(cmd =>
            {
                var health = _entities.GetComponent<HealthComponent>(cmd.Target);
                health.Current = Math.Min(health.Max, health.Current + cmd.Amount);
                _entities.SetComponent(cmd.Target, health);
            });

            var controller = new TestCombatController();
            controller.Construct(_container);
            controller.Initialize();

            // Act: Send multiple commands
            controller.SendDamage(entity, 30f);  // 100 - 30 = 70
            controller.SendHeal(entity, 15f);    // 70 + 15 = 85
            controller.SendDamage(entity, 50f);  // 85 - 50 = 35
            controller.SendHeal(entity, 100f);   // 35 + 100 = 100 (capped at max)

            // Assert: Final state is correct
            var finalHealth = _entities.GetComponent<HealthComponent>(entity);
            Assert.AreEqual(100f, finalHealth.Current);

            // Cleanup
            controller.Dispose();
        }

        #endregion

        #region Test Components and Commands

        private struct HealthComponent : IComponent
        {
            public float Current;
            public float Max;
        }

        private struct DamageCommand
        {
            public Entity Target;
            public float Amount;
        }

        private struct HealCommand
        {
            public Entity Target;
            public float Amount;
        }

        #endregion

        #region Test Views

        private class TestHealthView : StradaView
        {
            public float DisplayedHealth { get; private set; }

            public void UpdateHealth(float health)
            {
                DisplayedHealth = health;
            }
        }

        #endregion

        #region Test Controllers

        private class TestHealthController : StradaController
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

        private class TestHealthControllerWithView : StradaController
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

        private class TestDamageController : StradaController
        {
            public void SendDamageCommand(Entity target, float amount)
            {
                Send(new DamageCommand { Target = target, Amount = amount });
            }
        }

        private class TestCombatController : StradaController
        {
            public void SendDamage(Entity target, float amount)
            {
                Send(new DamageCommand { Target = target, Amount = amount });
            }

            public void SendHeal(Entity target, float amount)
            {
                Send(new HealCommand { Target = target, Amount = amount });
            }
        }

        #endregion

        #region Test Mediators

        private class TestHealthMediator : ViewMediator<TestHealthView>
        {
            protected override void OnBind()
            {
                Bind<HealthComponent, float>(
                    c => c.Current,
                    v => View.UpdateHealth(v));
            }

            protected override void OnUnbind() { }
        }

        private class TestHealthMediatorWithEventHandler : ViewMediator<TestHealthView>
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

        #endregion
    }
}
