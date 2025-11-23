using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Baking;
using System;
using UnityEngine;

namespace Strada.Core.Tests.ECS.Baking
{
    /// <summary>
    /// Tests for the baking pipeline.
    /// </summary>
    [TestFixture]
    public class BakingTests
    {
        private IStradaWorld _world;
        private IEntityManager _entityManager;

        [SetUp]
        public void Setup()
        {
            _world = StradaWorld.Create("BakingTestWorld");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
        }

        #region Baker Creation Tests

        [Test]
        public void CreateBaker_WithValidType_Succeeds()
        {
            // Act
            var baker = BakingUtility.CreateBaker(typeof(TestBaker));

            // Assert
            Assert.IsNotNull(baker);
            Assert.IsInstanceOf<TestBaker>(baker);
        }

        [Test]
        public void CreateBaker_WithNullType_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => BakingUtility.CreateBaker(null));
        }

        [Test]
        public void GetAuthoringType_ReturnsCorrectType()
        {
            // Act
            var authoringType = BakingUtility.GetAuthoringType(typeof(TestBaker));

            // Assert
            Assert.AreEqual(typeof(TestAuthoring), authoringType);
        }

        #endregion

        #region Baker Validation Tests

        [Test]
        public void ValidateBaker_WithValidBaker_ReturnsTrue()
        {
            // Act
            var isValid = BakingUtility.ValidateBaker(typeof(TestBaker), out var errorMessage);

            // Assert
            Assert.IsTrue(isValid);
            Assert.IsNull(errorMessage);
        }

        [Test]
        public void ValidateBaker_WithNullType_ReturnsFalse()
        {
            // Act
            var isValid = BakingUtility.ValidateBaker(null, out var errorMessage);

            // Assert
            Assert.IsFalse(isValid);
            Assert.IsNotNull(errorMessage);
        }

        [Test]
        public void ValidateBaker_WithAbstractType_ReturnsFalse()
        {
            // Act
            var isValid = BakingUtility.ValidateBaker(typeof(AbstractBaker), out var errorMessage);

            // Assert
            Assert.IsFalse(isValid);
            Assert.IsNotNull(errorMessage);
            Assert.That(errorMessage, Does.Contain("abstract"));
        }

        #endregion

        #region Baker Registry Tests

        [Test]
        public void BakerRegistry_Register_AddsBaker()
        {
            // Arrange
            var registry = new BakerRegistry();
            var baker = new TestBaker();

            // Act
            registry.Register(baker);

            // Assert
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void BakerRegistry_Register_SameBakerTwice_AddsOnce()
        {
            // Arrange
            var registry = new BakerRegistry();
            var baker = new TestBaker();

            // Act
            registry.Register(baker);
            registry.Register(baker);

            // Assert
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void BakerRegistry_GetBakersForType_ReturnsCorrectBakers()
        {
            // Arrange
            var registry = new BakerRegistry();
            var baker = new TestBaker();
            registry.Register(baker);

            // Act
            var bakers = registry.GetBakersForType(typeof(TestAuthoring));

            // Assert
            Assert.AreEqual(1, bakers.Count);
            Assert.Contains(baker, bakers);
        }

        [Test]
        public void BakerRegistry_GetBakersForType_WithNoMatching_ReturnsEmpty()
        {
            // Arrange
            var registry = new BakerRegistry();

            // Act
            var bakers = registry.GetBakersForType(typeof(TestAuthoring));

            // Assert
            Assert.AreEqual(0, bakers.Count);
        }

        [Test]
        public void BakerRegistry_Clear_RemovesAllBakers()
        {
            // Arrange
            var registry = new BakerRegistry();
            registry.Register(new TestBaker());
            registry.Register(new AnotherTestBaker());

            // Act
            registry.Clear();

            // Assert
            Assert.AreEqual(0, registry.Count);
        }

        #endregion

        #region Baking Execution Tests

        [Test]
        public void Bake_WithValidAuthoring_CreatesComponents()
        {
            // Arrange
            var baker = new TestBaker();
            var authoring = new TestAuthoring { Value = 42 };
            var context = BakingUtility.CreateTestContext(_entityManager);

            // Act
            baker.Bake(authoring, context);

            // Assert
            var entity = context.GetEntity(TransformUsageFlags.None);
            Assert.IsTrue(_entityManager.HasComponent<TestComponent>(entity));

            var component = _entityManager.GetComponent<TestComponent>(entity);
            Assert.AreEqual(42, component.Value);
        }

        [Test]
        public void Bake_WithInvalidAuthoring_LogsError()
        {
            // Arrange
            var baker = new ValidatingBaker();
            var authoring = new TestAuthoring { Value = -1 }; // Invalid
            var context = BakingUtility.CreateTestContext(_entityManager);

            // Act
            var isValid = baker.Validate(authoring, out var errorMessage);

            // Assert
            Assert.IsFalse(isValid);
            Assert.IsNotNull(errorMessage);
        }

        [Test]
        public void Bake_WithMultipleComponents_AllAdded()
        {
            // Arrange
            var baker = new MultiComponentBaker();
            var authoring = new TestAuthoring { Value = 10 };
            var context = BakingUtility.CreateTestContext(_entityManager);

            // Act
            baker.Bake(authoring, context);

            // Assert
            var entity = context.GetEntity(TransformUsageFlags.None);
            Assert.IsTrue(_entityManager.HasComponent<TestComponent>(entity));
            Assert.IsTrue(_entityManager.HasComponent<AnotherComponent>(entity));
        }

        #endregion

        #region ScriptableObject Baker Tests

        [Test]
        public void ScriptableObjectBaker_WithNullAuthoring_ValidationFails()
        {
            // Arrange
            var baker = new TestScriptableObjectBaker();

            // Act
            var isValid = baker.Validate(null, out var errorMessage);

            // Assert
            Assert.IsFalse(isValid);
            Assert.IsNotNull(errorMessage);
        }

        #endregion

        #region Buffer Component Tests

        [Test]
        public void DynamicBuffer_Add_IncreasesLength()
        {
            // Arrange
            var buffer = new DynamicBuffer<BufferElement>();

            // Act
            buffer.Add(new BufferElement { Value = 1 });
            buffer.Add(new BufferElement { Value = 2 });

            // Assert
            Assert.AreEqual(2, buffer.Length);
        }

        [Test]
        public void DynamicBuffer_Indexer_ReturnsCorrectElement()
        {
            // Arrange
            var buffer = new DynamicBuffer<BufferElement>();
            buffer.Add(new BufferElement { Value = 10 });
            buffer.Add(new BufferElement { Value = 20 });

            // Act & Assert
            Assert.AreEqual(10, buffer[0].Value);
            Assert.AreEqual(20, buffer[1].Value);
        }

        [Test]
        public void DynamicBuffer_Clear_ResetsLength()
        {
            // Arrange
            var buffer = new DynamicBuffer<BufferElement>();
            buffer.Add(new BufferElement { Value = 1 });
            buffer.Add(new BufferElement { Value = 2 });

            // Act
            buffer.Clear();

            // Assert
            Assert.AreEqual(0, buffer.Length);
        }

        [Test]
        public void DynamicBuffer_Resize_ChangesLength()
        {
            // Arrange
            var buffer = new DynamicBuffer<BufferElement>();

            // Act
            buffer.Resize(5);

            // Assert
            Assert.AreEqual(5, buffer.Length);
        }

        #endregion

        #region BakingConfig Tests

        [Test]
        public void BakingConfig_CreateDefault_HasValidSettings()
        {
            // Act
            var config = BakingConfig.CreateDefault();

            // Assert
            Assert.IsTrue(config.AutoDiscoverBakers);
            Assert.IsTrue(config.ValidateBeforeBaking);
            Assert.IsTrue(config.Validate(out _));
        }

        [Test]
        public void BakingConfig_ShouldScanAssembly_WithIncludedPattern_ReturnsTrue()
        {
            // Arrange
            var config = BakingConfig.CreateDefault();

            // Act & Assert
            Assert.IsTrue(config.ShouldScanAssembly("Strada.Core"));
            Assert.IsTrue(config.ShouldScanAssembly("Game.Module"));
        }

        [Test]
        public void BakingConfig_ShouldScanAssembly_WithExcludedPattern_ReturnsFalse()
        {
            // Arrange
            var config = BakingConfig.CreateDefault();

            // Act & Assert
            Assert.IsFalse(config.ShouldScanAssembly("Unity.Entities"));
            Assert.IsFalse(config.ShouldScanAssembly("System.Core"));
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

        private struct BufferElement : IBufferComponent
        {
            public int Value;
        }

        #endregion

        #region Test Authoring

        private class TestAuthoring
        {
            public int Value;
        }

        private class TestScriptableObjectAuthoring : ScriptableObject
        {
            public int Value;
        }

        #endregion

        #region Test Bakers

        [StradaBaker]
        private class TestBaker : StradaBaker<TestAuthoring>
        {
            public override void Bake(TestAuthoring authoring, IBakerContext context)
            {
                var entity = context.GetEntity(TransformUsageFlags.None);
                context.AddComponent(entity, new TestComponent { Value = authoring.Value });
            }
        }

        [StradaBaker]
        private class AnotherTestBaker : StradaBaker<TestAuthoring>
        {
            public override void Bake(TestAuthoring authoring, IBakerContext context)
            {
                var entity = context.GetEntity(TransformUsageFlags.None);
                context.AddComponent(entity, new AnotherComponent { Data = authoring.Value * 2.0f });
            }
        }

        [StradaBaker]
        private class ValidatingBaker : StradaBaker<TestAuthoring>
        {
            protected override bool ValidateAuthoring(TestAuthoring authoring, out string errorMessage)
            {
                if (authoring.Value < 0)
                {
                    errorMessage = "Value must be non-negative";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            public override void Bake(TestAuthoring authoring, IBakerContext context)
            {
                var entity = context.GetEntity(TransformUsageFlags.None);
                context.AddComponent(entity, new TestComponent { Value = authoring.Value });
            }
        }

        [StradaBaker]
        private class MultiComponentBaker : StradaBaker<TestAuthoring>
        {
            public override void Bake(TestAuthoring authoring, IBakerContext context)
            {
                var entity = context.GetEntity(TransformUsageFlags.None);
                context.AddComponent(entity, new TestComponent { Value = authoring.Value });
                context.AddComponent(entity, new AnotherComponent { Data = authoring.Value * 0.5f });
            }
        }

        [StradaBaker]
        private class TestScriptableObjectBaker : StradaScriptableObjectBaker<TestScriptableObjectAuthoring>
        {
            public override void Bake(TestScriptableObjectAuthoring authoring, IBakerContext context)
            {
                var entity = context.GetEntity(TransformUsageFlags.None);
                context.AddComponent(entity, new TestComponent { Value = authoring.Value });
            }
        }

        private abstract class AbstractBaker : StradaBaker<TestAuthoring>
        {
            public override void Bake(TestAuthoring authoring, IBakerContext context)
            {
                throw new NotImplementedException();
            }
        }

        #endregion
    }
}
