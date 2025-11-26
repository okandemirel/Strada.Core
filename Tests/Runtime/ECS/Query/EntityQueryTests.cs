using System.Collections.Generic;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Query;
using Strada.Core.ECS.Storage;

namespace Strada.Core.Tests.ECS.Query
{
    public struct PositionComponent : IComponent
    {
        public float X;
        public float Y;
        public float Z;
    }

    public struct VelocityComponent : IComponent
    {
        public float VX;
        public float VY;
        public float VZ;
    }

    public struct HealthComponent : IComponent
    {
        public int Current;
        public int Max;
    }

    public struct DamageComponent : IComponent { public int Value; }
    public struct ArmorComponent : IComponent { public int Value; }
    public struct SpeedComponent : IComponent { public float Value; }
    public struct StaminaComponent : IComponent { public float Value; }
    public struct ManaComponent : IComponent { public float Value; }

    [TestFixture]
    public class EntityQueryTests
    {
        private EntityManager _entityManager;

        [SetUp]
        public void Setup()
        {
            _entityManager = new EntityManager();
        }

        [TearDown]
        public void TearDown()
        {
            _entityManager?.Dispose();
        }

        [Test]
        public void SingleComponentQuery_IteratesAllEntities()
        {
            var entities = new List<Entity>();
            for (int i = 0; i < 100; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new PositionComponent { X = i, Y = i * 2, Z = i * 3 });
                entities.Add(entity);
            }

            int count = 0;
            float sumX = 0;

            _entityManager.ForEach<PositionComponent>((int entityIndex, ref PositionComponent pos) =>
            {
                count++;
                sumX += pos.X;
            });

            Assert.AreEqual(100, count);
            Assert.AreEqual(4950f, sumX);
        }

        [Test]
        public void SingleComponentQuery_ModifiesComponents()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity, new PositionComponent { X = 10, Y = 20, Z = 30 });

            _entityManager.ForEach<PositionComponent>((int entityIndex, ref PositionComponent pos) =>
            {
                pos.X *= 2;
                pos.Y *= 2;
                pos.Z *= 2;
            });

            var result = _entityManager.GetComponent<PositionComponent>(entity);
            Assert.AreEqual(20f, result.X);
            Assert.AreEqual(40f, result.Y);
            Assert.AreEqual(60f, result.Z);
        }

        [Test]
        public void TwoComponentQuery_OnlyIteratesEntitiesWithBoth()
        {
            var entityBoth = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityBoth, new PositionComponent { X = 1 });
            _entityManager.AddComponent(entityBoth, new VelocityComponent { VX = 10 });

            var entityPosOnly = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityPosOnly, new PositionComponent { X = 2 });

            var entityVelOnly = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityVelOnly, new VelocityComponent { VX = 20 });

            int count = 0;

            _entityManager.ForEach<PositionComponent, VelocityComponent>(
                (int entityIndex, ref PositionComponent pos, ref VelocityComponent vel) =>
                {
                    count++;
                    Assert.AreEqual(1f, pos.X);
                    Assert.AreEqual(10f, vel.VX);
                });

            Assert.AreEqual(1, count);
        }

        [Test]
        public void ThreeComponentQuery_OnlyIteratesEntitiesWithAll()
        {
            var entityAll = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityAll, new PositionComponent { X = 1 });
            _entityManager.AddComponent(entityAll, new VelocityComponent { VX = 10 });
            _entityManager.AddComponent(entityAll, new HealthComponent { Current = 100, Max = 100 });

            var entityTwo = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityTwo, new PositionComponent { X = 2 });
            _entityManager.AddComponent(entityTwo, new VelocityComponent { VX = 20 });

            int count = 0;

            _entityManager.ForEach<PositionComponent, VelocityComponent, HealthComponent>(
                (int entityIndex, ref PositionComponent pos, ref VelocityComponent vel, ref HealthComponent health) =>
                {
                    count++;
                    Assert.AreEqual(1f, pos.X);
                    Assert.AreEqual(10f, vel.VX);
                    Assert.AreEqual(100, health.Current);
                });

            Assert.AreEqual(1, count);
        }

        [Test]
        public void QueryBuilder_ReturnsCorrectCount()
        {
            for (int i = 0; i < 50; i++)
            {
                var entity = _entityManager.CreateEntity();
                _entityManager.AddComponent(entity, new PositionComponent());
            }

            var query = _entityManager.Query().With<PositionComponent>();
            Assert.AreEqual(50, query.Count);
        }

        [Test]
        public void Query_EmptyResult_DoesNotThrow()
        {
            int count = 0;

            _entityManager.ForEach<PositionComponent>((int entityIndex, ref PositionComponent pos) =>
            {
                count++;
            });

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Query_AfterEntityDestruction_ExcludesDestroyed()
        {
            var entity1 = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity1, new PositionComponent { X = 1 });

            var entity2 = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity2, new PositionComponent { X = 2 });

            _entityManager.DestroyEntity(entity1);

            int count = 0;
            float sumX = 0;

            _entityManager.ForEach<PositionComponent>((int entityIndex, ref PositionComponent pos) =>
            {
                count++;
                sumX += pos.X;
            });

            Assert.AreEqual(1, count);
            Assert.AreEqual(2f, sumX);
        }

        [Test]
        public void FourComponentQuery_OnlyIteratesEntitiesWithAll()
        {
            var entityAll = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityAll, new PositionComponent { X = 1 });
            _entityManager.AddComponent(entityAll, new VelocityComponent { VX = 2 });
            _entityManager.AddComponent(entityAll, new HealthComponent { Current = 3 });
            _entityManager.AddComponent(entityAll, new DamageComponent { Value = 4 });

            var entityThree = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityThree, new PositionComponent { X = 10 });
            _entityManager.AddComponent(entityThree, new VelocityComponent { VX = 20 });
            _entityManager.AddComponent(entityThree, new HealthComponent { Current = 30 });

            int count = 0;

            _entityManager.ForEach<PositionComponent, VelocityComponent, HealthComponent, DamageComponent>(
                (int entityIndex, ref PositionComponent pos, ref VelocityComponent vel, ref HealthComponent health, ref DamageComponent dmg) =>
                {
                    count++;
                    Assert.AreEqual(1f, pos.X);
                    Assert.AreEqual(2f, vel.VX);
                    Assert.AreEqual(3, health.Current);
                    Assert.AreEqual(4, dmg.Value);
                });

            Assert.AreEqual(1, count);
        }

        [Test]
        public void FiveComponentQuery_OnlyIteratesEntitiesWithAll()
        {
            var entityAll = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityAll, new PositionComponent { X = 1 });
            _entityManager.AddComponent(entityAll, new VelocityComponent { VX = 2 });
            _entityManager.AddComponent(entityAll, new HealthComponent { Current = 3 });
            _entityManager.AddComponent(entityAll, new DamageComponent { Value = 4 });
            _entityManager.AddComponent(entityAll, new ArmorComponent { Value = 5 });

            int count = 0;

            _entityManager.ForEach<PositionComponent, VelocityComponent, HealthComponent, DamageComponent, ArmorComponent>(
                (int entityIndex, ref PositionComponent pos, ref VelocityComponent vel, ref HealthComponent health,
                 ref DamageComponent dmg, ref ArmorComponent armor) =>
                {
                    count++;
                    Assert.AreEqual(1f, pos.X);
                    Assert.AreEqual(5, armor.Value);
                });

            Assert.AreEqual(1, count);
        }

        [Test]
        public void SixComponentQuery_OnlyIteratesEntitiesWithAll()
        {
            var entityAll = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityAll, new PositionComponent { X = 1 });
            _entityManager.AddComponent(entityAll, new VelocityComponent { VX = 2 });
            _entityManager.AddComponent(entityAll, new HealthComponent { Current = 3 });
            _entityManager.AddComponent(entityAll, new DamageComponent { Value = 4 });
            _entityManager.AddComponent(entityAll, new ArmorComponent { Value = 5 });
            _entityManager.AddComponent(entityAll, new SpeedComponent { Value = 6 });

            int count = 0;

            _entityManager.ForEach<PositionComponent, VelocityComponent, HealthComponent, DamageComponent, ArmorComponent, SpeedComponent>(
                (int entityIndex, ref PositionComponent pos, ref VelocityComponent vel, ref HealthComponent health,
                 ref DamageComponent dmg, ref ArmorComponent armor, ref SpeedComponent speed) =>
                {
                    count++;
                    Assert.AreEqual(6f, speed.Value);
                });

            Assert.AreEqual(1, count);
        }

        [Test]
        public void SevenComponentQuery_OnlyIteratesEntitiesWithAll()
        {
            var entityAll = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityAll, new PositionComponent { X = 1 });
            _entityManager.AddComponent(entityAll, new VelocityComponent { VX = 2 });
            _entityManager.AddComponent(entityAll, new HealthComponent { Current = 3 });
            _entityManager.AddComponent(entityAll, new DamageComponent { Value = 4 });
            _entityManager.AddComponent(entityAll, new ArmorComponent { Value = 5 });
            _entityManager.AddComponent(entityAll, new SpeedComponent { Value = 6 });
            _entityManager.AddComponent(entityAll, new StaminaComponent { Value = 7 });

            int count = 0;

            _entityManager.ForEach<PositionComponent, VelocityComponent, HealthComponent, DamageComponent, ArmorComponent, SpeedComponent, StaminaComponent>(
                (int entityIndex, ref PositionComponent pos, ref VelocityComponent vel, ref HealthComponent health,
                 ref DamageComponent dmg, ref ArmorComponent armor, ref SpeedComponent speed, ref StaminaComponent stamina) =>
                {
                    count++;
                    Assert.AreEqual(7f, stamina.Value);
                });

            Assert.AreEqual(1, count);
        }

        [Test]
        public void EightComponentQuery_OnlyIteratesEntitiesWithAll()
        {
            var entityAll = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityAll, new PositionComponent { X = 1 });
            _entityManager.AddComponent(entityAll, new VelocityComponent { VX = 2 });
            _entityManager.AddComponent(entityAll, new HealthComponent { Current = 3 });
            _entityManager.AddComponent(entityAll, new DamageComponent { Value = 4 });
            _entityManager.AddComponent(entityAll, new ArmorComponent { Value = 5 });
            _entityManager.AddComponent(entityAll, new SpeedComponent { Value = 6 });
            _entityManager.AddComponent(entityAll, new StaminaComponent { Value = 7 });
            _entityManager.AddComponent(entityAll, new ManaComponent { Value = 8 });

            int count = 0;

            _entityManager.ForEach<PositionComponent, VelocityComponent, HealthComponent, DamageComponent, ArmorComponent, SpeedComponent, StaminaComponent, ManaComponent>(
                (int entityIndex, ref PositionComponent pos, ref VelocityComponent vel, ref HealthComponent health,
                 ref DamageComponent dmg, ref ArmorComponent armor, ref SpeedComponent speed, ref StaminaComponent stamina, ref ManaComponent mana) =>
                {
                    count++;
                    Assert.AreEqual(8f, mana.Value);
                });

            Assert.AreEqual(1, count);
        }
    }
}
