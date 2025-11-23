using NUnit.Framework;
using Strada.Core.ECS;
using System;

namespace Strada.Core.Tests.ECS
{
    /// <summary>
    /// Tests for IEntityManager functionality.
    /// </summary>
    [TestFixture]
    public class EntityManagerTests
    {
        private IStradaWorld _world;
        private IEntityManager _entityManager;

        [SetUp]
        public void Setup()
        {
            _world = StradaWorld.Create("TestWorld");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        #region Entity Creation Tests

        [Test]
        public void CreateEntity_CreatesValidEntity()
        {
            // Act
            var entity = _entityManager.CreateEntity();

            // Assert
            Assert.AreNotEqual(Entity.Null, entity);
            Assert.Greater(entity.Index, 0);
            Assert.Greater(entity.Version, 0);
        }

        [Test]
        public void CreateEntity_MultipleTimes_CreatesUniqueEntities()
        {
            // Act
            var entity1 = _entityManager.CreateEntity();
            var entity2 = _entityManager.CreateEntity();
            var entity3 = _entityManager.CreateEntity();

            // Assert
            Assert.AreNotEqual(entity1, entity2);
            Assert.AreNotEqual(entity2, entity3);
            Assert.AreNotEqual(entity1, entity3);
        }

        [Test]
        public void CreateEntity_IncreasesEntityIndex()
        {
            // Act
            var entity1 = _entityManager.CreateEntity();
            var entity2 = _entityManager.CreateEntity();

            // Assert
            Assert.Less(entity1.Index, entity2.Index);
        }

        #endregion

        #region Entity Existence Tests

        [Test]
        public void Exists_WithCreatedEntity_ReturnsTrue()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();

            // Act & Assert
            Assert.IsTrue(_entityManager.Exists(entity));
        }

        [Test]
        public void Exists_WithDestroyedEntity_ReturnsFalse()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();
            _entityManager.DestroyEntity(entity);

            // Act & Assert
            Assert.IsFalse(_entityManager.Exists(entity));
        }

        [Test]
        public void Exists_WithNullEntity_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(_entityManager.Exists(Entity.Null));
        }

        #endregion

        #region Entity Destruction Tests

        [Test]
        public void DestroyEntity_RemovesEntity()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();

            // Act
            _entityManager.DestroyEntity(entity);

            // Assert
            Assert.IsFalse(_entityManager.Exists(entity));
        }

        [Test]
        public void DestroyEntity_RemovesAllComponents()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TestComponent>(entity);

            // Act
            _entityManager.DestroyEntity(entity);

            // Assert - entity no longer exists
            Assert.IsFalse(_entityManager.Exists(entity));
        }

        #endregion

        #region Component Add Tests

        [Test]
        public void AddComponent_ToEntity_Succeeds()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();

            // Act
            _entityManager.AddComponent<TestComponent>(entity);

            // Assert
            Assert.IsTrue(_entityManager.HasComponent<TestComponent>(entity));
        }

        [Test]
        public void AddComponent_ToNonexistentEntity_ThrowsException()
        {
            // Arrange
            var entity = new Entity { Index = 999, Version = 1 };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _entityManager.AddComponent<TestComponent>(entity));
        }

        [Test]
        public void AddComponent_MultipleComponents_AllAdded()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();

            // Act
            _entityManager.AddComponent<TestComponent>(entity);
            _entityManager.AddComponent<AnotherComponent>(entity);

            // Assert
            Assert.IsTrue(_entityManager.HasComponent<TestComponent>(entity));
            Assert.IsTrue(_entityManager.HasComponent<AnotherComponent>(entity));
        }

        #endregion

        #region Component Remove Tests

        [Test]
        public void RemoveComponent_FromEntity_Succeeds()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TestComponent>(entity);

            // Act
            _entityManager.RemoveComponent<TestComponent>(entity);

            // Assert
            Assert.IsFalse(_entityManager.HasComponent<TestComponent>(entity));
        }

        [Test]
        public void RemoveComponent_ComponentNotPresent_DoesNotThrow()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();

            // Act & Assert
            Assert.DoesNotThrow(() => _entityManager.RemoveComponent<TestComponent>(entity));
        }

        [Test]
        public void RemoveComponent_LeavesOtherComponents()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TestComponent>(entity);
            _entityManager.AddComponent<AnotherComponent>(entity);

            // Act
            _entityManager.RemoveComponent<TestComponent>(entity);

            // Assert
            Assert.IsFalse(_entityManager.HasComponent<TestComponent>(entity));
            Assert.IsTrue(_entityManager.HasComponent<AnotherComponent>(entity));
        }

        #endregion

        #region Component Has Tests

        [Test]
        public void HasComponent_WithComponent_ReturnsTrue()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TestComponent>(entity);

            // Act & Assert
            Assert.IsTrue(_entityManager.HasComponent<TestComponent>(entity));
        }

        [Test]
        public void HasComponent_WithoutComponent_ReturnsFalse()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();

            // Act & Assert
            Assert.IsFalse(_entityManager.HasComponent<TestComponent>(entity));
        }

        [Test]
        public void HasComponent_WithNonexistentEntity_ReturnsFalse()
        {
            // Arrange
            var entity = new Entity { Index = 999, Version = 1 };

            // Act & Assert
            Assert.IsFalse(_entityManager.HasComponent<TestComponent>(entity));
        }

        #endregion

        #region Component Get/Set Tests

        [Test]
        public void SetComponent_UpdatesComponentData()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TestComponent>(entity);

            var component = new TestComponent { Value = 42 };

            // Act
            _entityManager.SetComponent(entity, component);

            // Assert
            var retrieved = _entityManager.GetComponent<TestComponent>(entity);
            Assert.AreEqual(42, retrieved.Value);
        }

        [Test]
        public void GetComponent_WithComponent_ReturnsComponentData()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent<TestComponent>(entity);
            _entityManager.SetComponent(entity, new TestComponent { Value = 123 });

            // Act
            var component = _entityManager.GetComponent<TestComponent>(entity);

            // Assert
            Assert.AreEqual(123, component.Value);
        }

        [Test]
        public void GetComponent_WithoutComponent_ThrowsException()
        {
            // Arrange
            var entity = _entityManager.CreateEntity();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _entityManager.GetComponent<TestComponent>(entity));
        }

        [Test]
        public void GetComponent_WithNonexistentEntity_ThrowsException()
        {
            // Arrange
            var entity = new Entity { Index = 999, Version = 1 };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _entityManager.GetComponent<TestComponent>(entity));
        }

        [Test]
        public void SetComponent_WithNonexistentEntity_ThrowsException()
        {
            // Arrange
            var entity = new Entity { Index = 999, Version = 1 };
            var component = new TestComponent { Value = 42 };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _entityManager.SetComponent(entity, component));
        }

        #endregion

        #region Component Value Isolation Tests

        [Test]
        public void SetComponent_OnDifferentEntities_ValuesAreIndependent()
        {
            // Arrange
            var entity1 = _entityManager.CreateEntity();
            var entity2 = _entityManager.CreateEntity();
            _entityManager.AddComponent<TestComponent>(entity1);
            _entityManager.AddComponent<TestComponent>(entity2);

            // Act
            _entityManager.SetComponent(entity1, new TestComponent { Value = 10 });
            _entityManager.SetComponent(entity2, new TestComponent { Value = 20 });

            // Assert
            Assert.AreEqual(10, _entityManager.GetComponent<TestComponent>(entity1).Value);
            Assert.AreEqual(20, _entityManager.GetComponent<TestComponent>(entity2).Value);
        }

        #endregion

        #region Test Components

        private struct TestComponent : IStradaComponent
        {
            public int Value;
        }

        private struct AnotherComponent : IStradaComponent
        {
            public float Data;
        }

        #endregion
    }
}
