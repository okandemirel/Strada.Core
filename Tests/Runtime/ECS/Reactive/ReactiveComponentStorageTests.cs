using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Reactive;

namespace Strada.Core.Tests.ECS.Reactive
{
    [TestFixture]
    public sealed class ReactiveComponentStorageTests
    {
        private struct HealthComponent : IComponent
        {
            public int Value;
        }

        [Test]
        public void Add_TriggersOnAddCallback()
        {
            var storage = new ReactiveComponentStorage<HealthComponent>();
            var addedEntity = -1;
            var addedComponent = new HealthComponent();

            storage.SubscribeOnAdd((entity, component) =>
            {
                addedEntity = entity;
                addedComponent = component;
            });

            storage.Add(5, new HealthComponent { Value = 100 });

            Assert.AreEqual(5, addedEntity);
            Assert.AreEqual(100, addedComponent.Value);

            storage.Dispose();
        }

        [Test]
        public void Add_DoesNotTrigger_WhenEntityAlreadyExists()
        {
            var storage = new ReactiveComponentStorage<HealthComponent>();
            var addCount = 0;

            storage.SubscribeOnAdd((entity, component) => addCount++);

            storage.Add(5, new HealthComponent { Value = 100 });
            storage.Add(5, new HealthComponent { Value = 200 });

            Assert.AreEqual(1, addCount);

            storage.Dispose();
        }

        [Test]
        public void Remove_TriggersOnRemoveCallback()
        {
            var storage = new ReactiveComponentStorage<HealthComponent>();
            var removedEntity = -1;
            var removedComponent = new HealthComponent();

            storage.SubscribeOnRemove((entity, component) =>
            {
                removedEntity = entity;
                removedComponent = component;
            });

            storage.Add(5, new HealthComponent { Value = 100 });
            storage.Remove(5);

            Assert.AreEqual(5, removedEntity);
            Assert.AreEqual(100, removedComponent.Value);

            storage.Dispose();
        }

        [Test]
        public void Remove_ReturnsFalse_WhenEntityNotExists()
        {
            var storage = new ReactiveComponentStorage<HealthComponent>();
            var removeCount = 0;

            storage.SubscribeOnRemove((entity, component) => removeCount++);

            var result = storage.Remove(99);

            Assert.IsFalse(result);
            Assert.AreEqual(0, removeCount);

            storage.Dispose();
        }

        [Test]
        public void Set_TriggersOnChangeCallback_WhenEntityExists()
        {
            var storage = new ReactiveComponentStorage<HealthComponent>();
            var changedEntity = -1;
            var oldValue = new HealthComponent();
            var newValue = new HealthComponent();

            storage.SubscribeOnChange((entity, old, updated) =>
            {
                changedEntity = entity;
                oldValue = old;
                newValue = updated;
            });

            storage.Add(5, new HealthComponent { Value = 100 });
            storage.Set(5, new HealthComponent { Value = 50 });

            Assert.AreEqual(5, changedEntity);
            Assert.AreEqual(100, oldValue.Value);
            Assert.AreEqual(50, newValue.Value);

            storage.Dispose();
        }

        [Test]
        public void Set_TriggersOnAdd_WhenEntityNotExists()
        {
            var storage = new ReactiveComponentStorage<HealthComponent>();
            var addCount = 0;
            var changeCount = 0;

            storage.SubscribeOnAdd((entity, component) => addCount++);
            storage.SubscribeOnChange((entity, old, updated) => changeCount++);

            storage.Set(5, new HealthComponent { Value = 100 });

            Assert.AreEqual(1, addCount);
            Assert.AreEqual(0, changeCount);

            storage.Dispose();
        }

        [Test]
        public void Unsubscribe_StopsCallbacks()
        {
            var storage = new ReactiveComponentStorage<HealthComponent>();
            var addCount = 0;

            void OnAdd(int entity, HealthComponent component) => addCount++;

            storage.SubscribeOnAdd(OnAdd);
            storage.Add(1, new HealthComponent { Value = 100 });
            Assert.AreEqual(1, addCount);

            storage.UnsubscribeOnAdd(OnAdd);
            storage.Add(2, new HealthComponent { Value = 200 });
            Assert.AreEqual(1, addCount);

            storage.Dispose();
        }

        [Test]
        public void MultipleSubscribers_AllReceiveCallbacks()
        {
            var storage = new ReactiveComponentStorage<HealthComponent>();
            var count1 = 0;
            var count2 = 0;
            var count3 = 0;

            storage.SubscribeOnAdd((e, c) => count1++);
            storage.SubscribeOnAdd((e, c) => count2++);
            storage.SubscribeOnAdd((e, c) => count3++);

            storage.Add(1, new HealthComponent { Value = 100 });

            Assert.AreEqual(1, count1);
            Assert.AreEqual(1, count2);
            Assert.AreEqual(1, count3);

            storage.Dispose();
        }
    }
}
