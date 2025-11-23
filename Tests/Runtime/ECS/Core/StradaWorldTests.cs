using NUnit.Framework;
using Strada.Core.ECS;
using System;

namespace Strada.Core.Tests.ECS
{
    /// <summary>
    /// Tests for StradaWorld functionality.
    /// </summary>
    [TestFixture]
    public class StradaWorldTests
    {
        #region World Creation Tests

        [Test]
        public void Create_WithValidName_CreatesWorld()
        {
            // Arrange & Act
            using var world = StradaWorld.Create("TestWorld");

            // Assert
            Assert.IsNotNull(world);
            Assert.AreEqual("TestWorld", world.Name);
            Assert.IsFalse(world.IsInitialized);
            Assert.IsFalse(world.IsDisposed);
        }

        [Test]
        public void Create_WithNullName_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => StradaWorld.Create(null));
        }

        [Test]
        public void Create_WithEmptyName_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => StradaWorld.Create(""));
        }

        [Test]
        public void EntityManager_AfterCreation_IsNotNull()
        {
            // Arrange & Act
            using var world = StradaWorld.Create("TestWorld");

            // Assert
            Assert.IsNotNull(world.EntityManager);
        }

        #endregion

        #region System Registration Tests

        [Test]
        public void RegisterSystem_WithValidSystem_Succeeds()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");

            // Act
            world.RegisterSystem<TestSystem>();

            // Assert
            Assert.AreEqual(1, world.SystemCount);
        }

        [Test]
        public void RegisterSystem_SameSystemTwice_RegistersOnce()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");

            // Act
            world.RegisterSystem<TestSystem>();
            world.RegisterSystem<TestSystem>();

            // Assert
            Assert.AreEqual(1, world.SystemCount);
        }

        [Test]
        public void RegisterSystem_MultipleSystemsInitializationOrder()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");

            // Act
            world.RegisterSystem<TestSystem>();
            world.RegisterSystem<AnotherTestSystem>();
            world.Initialize();

            // Assert - systems should be registered
            Assert.IsTrue(world.HasSystem<TestSystem>());
            Assert.IsTrue(world.HasSystem<AnotherTestSystem>());
        }

        [Test]
        public void RegisterSystem_AfterInitialization_ThrowsException()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.Initialize();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => world.RegisterSystem<TestSystem>());
        }

        [Test]
        public void RegisterSystem_AfterDisposal_ThrowsException()
        {
            // Arrange
            var world = StradaWorld.Create("TestWorld");
            world.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => world.RegisterSystem<TestSystem>());
        }

        #endregion

        #region Initialization Tests

        [Test]
        public void Initialize_WithNoSystems_Succeeds()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");

            // Act
            world.Initialize();

            // Assert
            Assert.IsTrue(world.IsInitialized);
        }

        [Test]
        public void Initialize_WithSystems_CallsOnCreate()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<TestSystem>();

            // Act
            world.Initialize();

            // Assert
            Assert.IsTrue(world.IsInitialized);
            Assert.IsTrue(world.HasSystem<TestSystem>());
        }

        [Test]
        public void Initialize_CalledTwice_ThrowsException()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.Initialize();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => world.Initialize());
        }

        [Test]
        public void Initialize_AfterDisposal_ThrowsException()
        {
            // Arrange
            var world = StradaWorld.Create("TestWorld");
            world.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => world.Initialize());
        }

        #endregion

        #region Update Tests

        [Test]
        public void Update_WithoutInitialization_ThrowsException()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => world.Update(0.016f));
        }

        [Test]
        public void Update_AfterInitialization_Succeeds()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<TestSystem>();
            world.Initialize();

            // Act & Assert
            Assert.DoesNotThrow(() => world.Update(0.016f));
        }

        [Test]
        public void Update_AfterDisposal_ThrowsException()
        {
            // Arrange
            var world = StradaWorld.Create("TestWorld");
            world.Initialize();
            world.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => world.Update(0.016f));
        }

        [Test]
        public void Update_CallsSystemOnUpdate()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<CountingSystem>();
            world.Initialize();

            // Act
            world.Update(0.016f);

            // Assert
            var system = world.GetSystem<CountingSystem>();
            Assert.AreEqual(1, system.UpdateCount);
        }

        [Test]
        public void Update_MultipleFrames_IncreasesUpdateCount()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<CountingSystem>();
            world.Initialize();

            // Act
            world.Update(0.016f);
            world.Update(0.016f);
            world.Update(0.016f);

            // Assert
            var system = world.GetSystem<CountingSystem>();
            Assert.AreEqual(3, system.UpdateCount);
        }

        #endregion

        #region System Query Tests

        [Test]
        public void GetSystem_WithRegisteredSystem_ReturnsSystem()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<TestSystem>();
            world.Initialize();

            // Act
            var system = world.GetSystem<TestSystem>();

            // Assert
            Assert.IsNotNull(system);
        }

        [Test]
        public void GetSystem_WithUnregisteredSystem_ThrowsException()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.Initialize();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => world.GetSystem<TestSystem>());
        }

        [Test]
        public void HasSystem_WithRegisteredSystem_ReturnsTrue()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<TestSystem>();
            world.Initialize();

            // Act & Assert
            Assert.IsTrue(world.HasSystem<TestSystem>());
        }

        [Test]
        public void HasSystem_WithUnregisteredSystem_ReturnsFalse()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.Initialize();

            // Act & Assert
            Assert.IsFalse(world.HasSystem<TestSystem>());
        }

        #endregion

        #region System Enable/Disable Tests

        [Test]
        public void SetSystemEnabled_DisablesSystem_SystemDoesNotUpdate()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<CountingSystem>();
            world.Initialize();

            // Act
            world.SetSystemEnabled<CountingSystem>(false);
            world.Update(0.016f);

            // Assert
            var system = world.GetSystem<CountingSystem>();
            Assert.AreEqual(0, system.UpdateCount);
        }

        [Test]
        public void SetSystemEnabled_ReenablesSystem_SystemUpdates()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<CountingSystem>();
            world.Initialize();

            // Act
            world.SetSystemEnabled<CountingSystem>(false);
            world.Update(0.016f);
            world.SetSystemEnabled<CountingSystem>(true);
            world.Update(0.016f);

            // Assert
            var system = world.GetSystem<CountingSystem>();
            Assert.AreEqual(1, system.UpdateCount);
        }

        [Test]
        public void SetSystemEnabled_WithUnregisteredSystem_ThrowsException()
        {
            // Arrange
            using var world = StradaWorld.Create("TestWorld");
            world.Initialize();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => world.SetSystemEnabled<TestSystem>(false));
        }

        #endregion

        #region Disposal Tests

        [Test]
        public void Dispose_MarksWorldAsDisposed()
        {
            // Arrange
            var world = StradaWorld.Create("TestWorld");
            world.Initialize();

            // Act
            world.Dispose();

            // Assert
            Assert.IsTrue(world.IsDisposed);
        }

        [Test]
        public void Dispose_CallsOnDestroyOnSystems()
        {
            // Arrange
            var world = StradaWorld.Create("TestWorld");
            world.RegisterSystem<TestSystem>();
            world.Initialize();

            // Act
            world.Dispose();

            // Assert - no exception thrown
            Assert.IsTrue(world.IsDisposed);
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            // Arrange
            var world = StradaWorld.Create("TestWorld");
            world.Initialize();

            // Act & Assert
            world.Dispose();
            Assert.DoesNotThrow(() => world.Dispose());
        }

        #endregion

        #region Test Systems

        [StradaSystem]
        private struct TestSystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state) { }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem]
        private struct AnotherTestSystem : IStradaSystem
        {
            public void OnCreate(ref SystemState state) { }
            public void OnUpdate(ref SystemState state) { }
            public void OnDestroy(ref SystemState state) { }
        }

        [StradaSystem]
        private struct CountingSystem : IStradaSystem
        {
            public int UpdateCount;

            public void OnCreate(ref SystemState state)
            {
                UpdateCount = 0;
            }

            public void OnUpdate(ref SystemState state)
            {
                UpdateCount++;
            }

            public void OnDestroy(ref SystemState state) { }
        }

        #endregion
    }
}
