using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Reactive;

namespace Strada.Core.Tests.Tests.Runtime.ECS.Reactive
{
    [TestFixture]
    public sealed class ReactiveEntityManagerTests
    {
        private struct PositionComponent : IComponent
        {
            public float X;
            public float Y;
        }

        private struct VelocityComponent : IComponent
        {
            public float X;
            public float Y;
        }

        [Test]
        public void OnAdd_RegistersCallbackCorrectly()
        {
            using var rem = new ReactiveEntityManager();
            var addedEntity = -1;
            var addedPosition = new PositionComponent();

            rem.OnAdd<PositionComponent>((entity, component) =>
            {
                addedEntity = entity;
                addedPosition = component;
            });

            var entity = rem.CreateEntity();
            rem.AddReactiveComponent(entity, new PositionComponent { X = 10, Y = 20 });

            Assert.AreEqual(entity.Index, addedEntity);
            Assert.AreEqual(10, addedPosition.X);
            Assert.AreEqual(20, addedPosition.Y);
        }

        [Test]
        public void OnRemove_RegistersCallbackCorrectly()
        {
            using var rem = new ReactiveEntityManager();
            var removedEntity = -1;

            rem.OnRemove<PositionComponent>((entity, component) => removedEntity = entity);

            var entity = rem.CreateEntity();
            rem.AddReactiveComponent(entity, new PositionComponent { X = 10, Y = 20 });
            rem.RemoveReactiveComponent<PositionComponent>(entity);

            Assert.AreEqual(entity.Index, removedEntity);
        }

        [Test]
        public void OnChange_RegistersCallbackCorrectly()
        {
            using var rem = new ReactiveEntityManager();
            var oldX = 0f;
            var newX = 0f;

            rem.OnChange<PositionComponent>((entity, oldVal, newVal) =>
            {
                oldX = oldVal.X;
                newX = newVal.X;
            });

            var entity = rem.CreateEntity();
            rem.AddReactiveComponent(entity, new PositionComponent { X = 10, Y = 20 });
            rem.SetReactiveComponent(entity, new PositionComponent { X = 30, Y = 40 });

            Assert.AreEqual(10, oldX);
            Assert.AreEqual(30, newX);
        }

        [Test]
        public void MultipleComponentTypes_IndependentCallbacks()
        {
            using var rem = new ReactiveEntityManager();
            var positionAdded = false;
            var velocityAdded = false;

            rem.OnAdd<PositionComponent>((e, c) => positionAdded = true);
            rem.OnAdd<VelocityComponent>((e, c) => velocityAdded = true);

            var entity = rem.CreateEntity();
            rem.AddReactiveComponent(entity, new PositionComponent { X = 10, Y = 20 });

            Assert.IsTrue(positionAdded);
            Assert.IsFalse(velocityAdded);

            rem.AddReactiveComponent(entity, new VelocityComponent { X = 1, Y = 2 });

            Assert.IsTrue(velocityAdded);
        }

        [Test]
        public void GetReactiveComponent_ReturnsCorrectValue()
        {
            using var rem = new ReactiveEntityManager();

            var entity = rem.CreateEntity();
            rem.AddReactiveComponent(entity, new PositionComponent { X = 100, Y = 200 });

            var position = rem.GetReactiveComponent<PositionComponent>(entity);

            Assert.AreEqual(100, position.X);
            Assert.AreEqual(200, position.Y);
        }

        [Test]
        public void GetReactiveStorage_ReturnsSameInstance()
        {
            using var rem = new ReactiveEntityManager();

            var storage1 = rem.GetReactiveStorage<PositionComponent>();
            var storage2 = rem.GetReactiveStorage<PositionComponent>();

            Assert.AreSame(storage1, storage2);
        }

        [Test]
        public void Dispose_CleansUpAllStorages()
        {
            var rem = new ReactiveEntityManager();

            var entity = rem.CreateEntity();
            rem.AddReactiveComponent(entity, new PositionComponent { X = 10, Y = 20 });
            rem.AddReactiveComponent(entity, new VelocityComponent { X = 1, Y = 2 });

            rem.Dispose();

            var newRem = new ReactiveEntityManager();
            var storage = newRem.GetReactiveStorage<PositionComponent>();
            Assert.AreEqual(0, storage.Count);
            newRem.Dispose();
        }
    }
}
