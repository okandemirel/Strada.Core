using NUnit.Framework;
using Strada.Core.ECS;
using System;
using System.Collections.Generic;

namespace Strada.Core.Tests.ECS
{
    /// <summary>
    /// Tests for system ordering and dependency management.
    /// </summary>
    [TestFixture]
    public class SystemOrderingTests
    {
        private static List<string> _executionOrder;

        [SetUp]
        public void Setup()
        {
            _executionOrder = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            _executionOrder?.Clear();
        }

        #region Update After Tests

        [Test]
        public void SystemOrdering_UpdateAfter_ExecutesInCorrectOrder()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<FirstSystem>();
            world.RegisterSystem<SecondSystemAfterFirst>();
            world.Initialize();

            // Act
            world.Update(0.016f);

            // Assert
            Assert.AreEqual(2, _executionOrder.Count);
            Assert.AreEqual("FirstSystem", _executionOrder[0]);
            Assert.AreEqual("SecondSystemAfterFirst", _executionOrder[1]);
        }

        [Test]
        public void SystemOrdering_UpdateAfterMultiple_ExecutesInCorrectOrder()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<FirstSystem>();
            world.RegisterSystem<SecondSystemAfterFirst>();
            world.RegisterSystem<ThirdSystemAfterSecond>();
            world.Initialize();

            // Act
            world.Update(0.016f);

            // Assert
            Assert.AreEqual(3, _executionOrder.Count);
            Assert.AreEqual("FirstSystem", _executionOrder[0]);
            Assert.AreEqual("SecondSystemAfterFirst", _executionOrder[1]);
            Assert.AreEqual("ThirdSystemAfterSecond", _executionOrder[2]);
        }

        #endregion

        #region Update Before Tests

        [Test]
        public void SystemOrdering_UpdateBefore_ExecutesInCorrectOrder()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<LastSystem>();
            world.RegisterSystem<SecondToLastSystemBeforeLast>();
            world.Initialize();

            // Act
            world.Update(0.016f);

            // Assert
            Assert.AreEqual(2, _executionOrder.Count);
            Assert.AreEqual("SecondToLastSystemBeforeLast", _executionOrder[0]);
            Assert.AreEqual("LastSystem", _executionOrder[1]);
        }

        #endregion

        #region Priority Tests

        [Test]
        public void SystemOrdering_Priority_LowerPriorityExecutesFirst()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<LowPrioritySystem>();
            world.RegisterSystem<HighPrioritySystem>();
            world.Initialize();

            // Act
            world.Update(0.016f);

            // Assert
            Assert.AreEqual(2, _executionOrder.Count);
            Assert.AreEqual("LowPrioritySystem", _executionOrder[0]);
            Assert.AreEqual("HighPrioritySystem", _executionOrder[1]);
        }

        #endregion

        #region Circular Dependency Tests

        [Test]
        public void SystemOrdering_CircularDependency_ThrowsException()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<CircularSystemA>();
            world.RegisterSystem<CircularSystemB>();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => world.Initialize());
        }

        #endregion

        #region Complex Ordering Tests

        [Test]
        public void SystemOrdering_ComplexDependencies_ExecutesCorrectly()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<InputSystem>();
            world.RegisterSystem<PhysicsSystem>();
            world.RegisterSystem<RenderSystem>();
            world.Initialize();

            // Act
            world.Update(0.016f);

            // Assert
            Assert.AreEqual(3, _executionOrder.Count);
            Assert.AreEqual("InputSystem", _executionOrder[0]);
            Assert.AreEqual("PhysicsSystem", _executionOrder[1]);
            Assert.AreEqual("RenderSystem", _executionOrder[2]);
        }

        #endregion

        #region Disabled System Tests

        [Test]
        public void SystemOrdering_DisabledSystem_DoesNotExecute()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<FirstSystem>();
            world.RegisterSystem<SecondSystemAfterFirst>();
            world.Initialize();
            world.SetSystemEnabled<SecondSystemAfterFirst>(false);

            // Act
            world.Update(0.016f);

            // Assert
            Assert.AreEqual(1, _executionOrder.Count);
            Assert.AreEqual("FirstSystem", _executionOrder[0]);
        }

        [Test]
        public void SystemOrdering_DisabledByDefault_DoesNotExecute()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<FirstSystem>();
            world.RegisterSystem<DisabledByDefaultSystem>();
            world.Initialize();

            // Act
            world.Update(0.016f);

            // Assert
            Assert.AreEqual(1, _executionOrder.Count);
            Assert.AreEqual("FirstSystem", _executionOrder[0]);
        }

        #endregion

        #region Test Systems - UpdateAfter

        [StradaSystem]
        private struct FirstSystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("FirstSystem");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem(UpdateAfter = new[] { typeof(FirstSystem) })]
        private struct SecondSystemAfterFirst : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("SecondSystemAfterFirst");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem(UpdateAfter = new[] { typeof(SecondSystemAfterFirst) })]
        private struct ThirdSystemAfterSecond : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("ThirdSystemAfterSecond");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        #endregion

        #region Test Systems - UpdateBefore

        [StradaSystem]
        private struct LastSystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("LastSystem");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem(UpdateBefore = new[] { typeof(LastSystem) })]
        private struct SecondToLastSystemBeforeLast : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("SecondToLastSystemBeforeLast");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        #endregion

        #region Test Systems - Priority

        [StradaSystem(Priority = 10)]
        private struct LowPrioritySystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("LowPrioritySystem");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem(Priority = 100)]
        private struct HighPrioritySystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("HighPrioritySystem");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        #endregion

        #region Test Systems - Circular Dependencies

        [StradaSystem(UpdateAfter = new[] { typeof(CircularSystemB) })]
        private struct CircularSystemA : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state) { }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem(UpdateAfter = new[] { typeof(CircularSystemA) })]
        private struct CircularSystemB : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state) { }
            public void OnDestroy(ref SystemState state) { }
        }

        #endregion

        #region Test Systems - Complex

        [StradaSystem(Priority = 10)]
        private struct InputSystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("InputSystem");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem(Priority = 20, UpdateAfter = new[] { typeof(InputSystem) })]
        private struct PhysicsSystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("PhysicsSystem");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem(Priority = 30, UpdateAfter = new[] { typeof(PhysicsSystem) })]
        private struct RenderSystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("RenderSystem");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        #endregion

        #region Test Systems - Disabled

        [StradaSystem(EnabledByDefault = false)]
        private struct DisabledByDefaultSystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state)
            {
                _executionOrder.Add("DisabledByDefaultSystem");
            }
            public void OnDestroy(ref SystemState state) { }
        }

        #endregion
    }
}
