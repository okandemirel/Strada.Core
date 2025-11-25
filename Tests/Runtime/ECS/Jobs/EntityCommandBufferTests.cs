using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Jobs;
using Unity.Collections;

namespace Strada.Core.Tests.ECS.Jobs
{
    [TestFixture]
    public class EntityCommandBufferTests
    {
        private EntityManager _entityManager;

        private struct Position : IComponent { public float X, Y, Z; }
        private struct Velocity : IComponent { public float X, Y, Z; }

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
            ComponentPlayback.EnsureHandler<Position>();
            ComponentPlayback.EnsureHandler<Velocity>();
        }

        [TearDown]
        public void TearDown()
        {
            _entityManager?.Dispose();
        }

        [Test]
        public void CreateEntity_PlaybackCreatesEntities()
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            ecb.CreateEntity();
            ecb.CreateEntity();
            ecb.CreateEntity();

            Assert.AreEqual(3, ecb.CreatedEntityCount);
            Assert.AreEqual(0, _entityManager.EntityCount);

            ecb.Playback(_entityManager);

            Assert.AreEqual(3, _entityManager.EntityCount);
            ecb.Dispose();
        }

        [Test]
        public void DestroyEntity_PlaybackDestroysEntities()
        {
            var entity1 = _entityManager.CreateEntity();
            var entity2 = _entityManager.CreateEntity();
            Assert.AreEqual(2, _entityManager.EntityCount);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            ecb.DestroyEntity(entity1);
            ecb.Playback(_entityManager);

            Assert.AreEqual(1, _entityManager.EntityCount);
            Assert.IsFalse(_entityManager.Exists(entity1));
            Assert.IsTrue(_entityManager.Exists(entity2));
            ecb.Dispose();
        }

        [Test]
        public void AddComponent_ToExistingEntity()
        {
            var entity = _entityManager.CreateEntity();

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            ecb.AddComponent(entity, new Position { X = 1, Y = 2, Z = 3 });
            ecb.Playback(_entityManager);

            Assert.IsTrue(_entityManager.HasComponent<Position>(entity));
            var pos = _entityManager.GetComponent<Position>(entity);
            Assert.AreEqual(1f, pos.X);
            Assert.AreEqual(2f, pos.Y);
            Assert.AreEqual(3f, pos.Z);
            ecb.Dispose();
        }

        [Test]
        public void AddComponent_ToDeferredEntity()
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            int deferredIndex = ecb.CreateEntity();
            ecb.AddComponent(deferredIndex, new Position { X = 10, Y = 20, Z = 30 });
            ecb.AddComponent(deferredIndex, new Velocity { X = 1, Y = 2, Z = 3 });

            ecb.Playback(_entityManager);

            Assert.AreEqual(1, _entityManager.EntityCount);

            var entities = _entityManager.GetAllEntities();
            Entity entity = default;
            foreach (var e in entities) { entity = new Entity(e, 1); break; }

            Assert.IsTrue(_entityManager.HasComponent<Position>(entity));
            Assert.IsTrue(_entityManager.HasComponent<Velocity>(entity));

            var pos = _entityManager.GetComponent<Position>(entity);
            Assert.AreEqual(10f, pos.X);
            ecb.Dispose();
        }

        [Test]
        public void SetComponent_UpdatesExistingComponent()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity, new Position { X = 0, Y = 0, Z = 0 });

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            ecb.SetComponent(entity, new Position { X = 100, Y = 200, Z = 300 });
            ecb.Playback(_entityManager);

            var pos = _entityManager.GetComponent<Position>(entity);
            Assert.AreEqual(100f, pos.X);
            Assert.AreEqual(200f, pos.Y);
            Assert.AreEqual(300f, pos.Z);
            ecb.Dispose();
        }

        [Test]
        public void RemoveComponent_RemovesFromEntity()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity, new Position());

            Assert.IsTrue(_entityManager.HasComponent<Position>(entity));

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            ecb.RemoveComponent<Position>(entity);
            ecb.Playback(_entityManager);

            Assert.IsFalse(_entityManager.HasComponent<Position>(entity));
            ecb.Dispose();
        }

        [Test]
        public void Clear_ResetsCommandBuffer()
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            ecb.CreateEntity();
            ecb.CreateEntity();
            Assert.AreEqual(2, ecb.CreatedEntityCount);

            ecb.Clear();

            Assert.AreEqual(0, ecb.CreatedEntityCount);
            Assert.AreEqual(0, ecb.CommandCount);

            ecb.Playback(_entityManager);
            Assert.AreEqual(0, _entityManager.EntityCount);
            ecb.Dispose();
        }

        [Test]
        public void MultiplePlaybacks_WorkCorrectly()
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            ecb.CreateEntity();
            ecb.Playback(_entityManager);
            Assert.AreEqual(1, _entityManager.EntityCount);

            ecb.Clear();
            ecb.CreateEntity();
            ecb.CreateEntity();
            ecb.Playback(_entityManager);
            Assert.AreEqual(3, _entityManager.EntityCount);

            ecb.Dispose();
        }

        [Test]
        public void ComplexSequence_WorksCorrectly()
        {
            var existingEntity = _entityManager.CreateEntity();
            _entityManager.AddComponent(existingEntity, new Position());

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            int newEntityIdx = ecb.CreateEntity();
            ecb.AddComponent(newEntityIdx, new Position { X = 5, Y = 5, Z = 5 });
            ecb.SetComponent(existingEntity, new Position { X = 99, Y = 99, Z = 99 });

            ecb.Playback(_entityManager);

            Assert.AreEqual(2, _entityManager.EntityCount);

            var existingPos = _entityManager.GetComponent<Position>(existingEntity);
            Assert.AreEqual(99f, existingPos.X);

            ecb.Dispose();
        }
    }
}
