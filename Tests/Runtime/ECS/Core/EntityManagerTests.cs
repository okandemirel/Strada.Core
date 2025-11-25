using NUnit.Framework;
using Strada.Core.ECS;

namespace Strada.Core.Tests.ECS.Core
{
    [TestFixture]
    public class EntityManagerTests
    {
        struct Position : IComponent
        {
            public float X;
            public float Y;
        }

        struct Velocity : IComponent
        {
            public float VX;
            public float VY;
        }

        struct Health : IComponent
        {
            public int Value;
        }

        [Test]
        public void CreateEntity_ReturnsValidEntity()
        {
            var manager = new EntityManager();

            var entity = manager.CreateEntity();

            Assert.AreNotEqual(0, entity.Index);
            Assert.IsTrue(manager.Exists(entity));

            manager.Dispose();
        }

        [Test]
        public void CreateMultipleEntities_UniqueIndices()
        {
            var manager = new EntityManager();

            var entity1 = manager.CreateEntity();
            var entity2 = manager.CreateEntity();
            var entity3 = manager.CreateEntity();

            Assert.AreNotEqual(entity1.Index, entity2.Index);
            Assert.AreNotEqual(entity2.Index, entity3.Index);
            Assert.AreNotEqual(entity1.Index, entity3.Index);

            manager.Dispose();
        }

        [Test]
        public void AddComponent_Success()
        {
            var manager = new EntityManager();
            var entity = manager.CreateEntity();

            manager.AddComponent(entity, new Position { X = 10, Y = 20 });

            Assert.IsTrue(manager.HasComponent<Position>(entity));
            var pos = manager.GetComponent<Position>(entity);
            Assert.AreEqual(10, pos.X);
            Assert.AreEqual(20, pos.Y);

            manager.Dispose();
        }

        [Test]
        public void RemoveComponent_Success()
        {
            var manager = new EntityManager();
            var entity = manager.CreateEntity();

            manager.AddComponent(entity, new Position { X = 10, Y = 20 });
            manager.RemoveComponent<Position>(entity);

            Assert.IsFalse(manager.HasComponent<Position>(entity));

            manager.Dispose();
        }

        [Test]
        public void GetComponent_ReturnsCorrectData()
        {
            var manager = new EntityManager();
            var entity = manager.CreateEntity();

            manager.AddComponent(entity, new Position { X = 10, Y = 20 });
            var pos = manager.GetComponent<Position>(entity);

            Assert.AreEqual(10, pos.X);
            Assert.AreEqual(20, pos.Y);

            manager.Dispose();
        }

        [Test]
        public void SetComponent_UpdatesData()
        {
            var manager = new EntityManager();
            var entity = manager.CreateEntity();

            manager.AddComponent(entity, new Position { X = 10, Y = 20 });
            manager.SetComponent(entity, new Position { X = 30, Y = 40 });

            var pos = manager.GetComponent<Position>(entity);
            Assert.AreEqual(30, pos.X);
            Assert.AreEqual(40, pos.Y);

            manager.Dispose();
        }

        [Test]
        public void DestroyEntity_RemovesEntity()
        {
            var manager = new EntityManager();
            var entity = manager.CreateEntity();

            manager.DestroyEntity(entity);

            Assert.IsFalse(manager.Exists(entity));

            manager.Dispose();
        }

        [Test]
        public void DestroyEntity_RemovesAllComponents()
        {
            var manager = new EntityManager();
            var entity = manager.CreateEntity();

            manager.AddComponent(entity, new Position { X = 10, Y = 20 });
            manager.AddComponent(entity, new Velocity { VX = 1, VY = 0 });
            manager.DestroyEntity(entity);

            Assert.IsFalse(manager.Exists(entity));

            manager.Dispose();
        }

        [Test]
        public void EntityCount_TracksCorrectly()
        {
            var manager = new EntityManager();

            Assert.AreEqual(0, manager.EntityCount);

            var e1 = manager.CreateEntity();
            Assert.AreEqual(1, manager.EntityCount);

            var e2 = manager.CreateEntity();
            Assert.AreEqual(2, manager.EntityCount);

            manager.DestroyEntity(e1);
            Assert.AreEqual(1, manager.EntityCount);

            manager.Dispose();
        }

        [Test]
        public void EntityIndexRecycling_ReusesIndices()
        {
            var manager = new EntityManager();

            var e1 = manager.CreateEntity();
            int index1 = e1.Index;

            manager.DestroyEntity(e1);

            var e2 = manager.CreateEntity();
            int index2 = e2.Index;

            Assert.AreEqual(index1, index2);

            manager.Dispose();
        }

        [Test]
        public void MultipleComponentTypes_WorkIndependently()
        {
            var manager = new EntityManager();

            var e1 = manager.CreateEntity();
            manager.AddComponent(e1, new Position { X = 1, Y = 2 });
            manager.AddComponent(e1, new Velocity { VX = 10, VY = 20 });
            manager.AddComponent(e1, new Health { Value = 100 });

            Assert.IsTrue(manager.HasComponent<Position>(e1));
            Assert.IsTrue(manager.HasComponent<Velocity>(e1));
            Assert.IsTrue(manager.HasComponent<Health>(e1));

            var pos = manager.GetComponent<Position>(e1);
            var vel = manager.GetComponent<Velocity>(e1);
            var health = manager.GetComponent<Health>(e1);

            Assert.AreEqual(1, pos.X);
            Assert.AreEqual(10, vel.VX);
            Assert.AreEqual(100, health.Value);

            manager.Dispose();
        }

        [Test]
        public void Clear_RemovesAllEntities()
        {
            var manager = new EntityManager();

            for (int i = 0; i < 10; i++)
            {
                var entity = manager.CreateEntity();
                manager.AddComponent(entity, new Position { X = i, Y = i * 2 });
            }

            Assert.AreEqual(10, manager.EntityCount);

            manager.Clear();

            Assert.AreEqual(0, manager.EntityCount);

            manager.Dispose();
        }
    }
}
