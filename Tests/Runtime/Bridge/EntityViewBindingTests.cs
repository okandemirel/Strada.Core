using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using UnityEngine;

namespace Strada.Core.Tests.Tests.Runtime.Bridge
{
    [TestFixture]
    public class EntityViewBindingTests
    {
        private EntityManager _entityManager;
        private IContainer _container;
        private GameObject _testObject;

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
            var builder = new ContainerBuilder();
            _container = builder.Build();
            _testObject = new GameObject("TestView");
        }

        [TearDown]
        public void TearDown()
        {
            _entityManager?.Dispose();
            _container?.Dispose();
            if (_testObject != null)
                Object.DestroyImmediate(_testObject);
        }

        [Test]
        public void EntityView_Bind_SetsEntity()
        {
            var view = _testObject.AddComponent<TestEntityView>();
            var entity = _entityManager.CreateEntity();

            view.Bind(_container, _entityManager, entity);

            Assert.AreEqual(entity.Index, view.Entity.Index);
            Assert.IsTrue(view.IsBound);
        }

        [Test]
        public void EntityView_Unbind_ClearsEntity()
        {
            var view = _testObject.AddComponent<TestEntityView>();
            var entity = _entityManager.CreateEntity();
            view.Bind(_container, _entityManager, entity);

            view.Unbind();

            Assert.IsFalse(view.IsBound);
        }

        [Test]
        public void EntityView_OnBind_Called()
        {
            var view = _testObject.AddComponent<TestEntityView>();
            var entity = _entityManager.CreateEntity();

            view.Bind(_container, _entityManager, entity);

            Assert.IsTrue(view.OnBindCalled);
        }

        [Test]
        public void EntityView_OnUnbind_Called()
        {
            var view = _testObject.AddComponent<TestEntityView>();
            var entity = _entityManager.CreateEntity();
            view.Bind(_container, _entityManager, entity);

            view.Unbind();

            Assert.IsTrue(view.OnUnbindCalled);
        }

        [Test]
        public void ComponentBinding_Create_ReadsExistingComponent()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity, new TestComponent { Value = 42 });

            var binding = new ComponentBinding<TestComponent>(_entityManager, entity);

            Assert.AreEqual(42, binding.Value.Value);
        }

        [Test]
        public void ComponentBinding_SetValue_UpdatesComponent()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity, new TestComponent { Value = 0 });

            var binding = new ComponentBinding<TestComponent>(_entityManager, entity);
            binding.Value = new TestComponent { Value = 100 };

            var component = _entityManager.GetComponent<TestComponent>(entity);
            Assert.AreEqual(100, component.Value);
        }

        [Test]
        public void ComponentBinding_OnChanged_Fires()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity, new TestComponent { Value = 0 });

            var binding = new ComponentBinding<TestComponent>(_entityManager, entity);

            int changeCount = 0;
            binding.OnChanged += _ => changeCount++;

            binding.Value = new TestComponent { Value = 50 };

            Assert.AreEqual(1, changeCount);
        }

        [Test]
        public void ComponentBinding_Sync_UpdatesCachedValue()
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponent(entity, new TestComponent { Value = 10 });

            var binding = new ComponentBinding<TestComponent>(_entityManager, entity);

            _entityManager.SetComponent(entity, new TestComponent { Value = 99 });

            binding.Sync();

            Assert.AreEqual(99, binding.Value.Value);
        }

        private struct TestComponent : IComponent
        {
            public int Value;
        }

        private class TestEntityView : EntityView
        {
            public bool OnBindCalled;
            public bool OnUnbindCalled;

            protected override void OnBind()
            {
                OnBindCalled = true;
            }

            protected override void OnUnbind()
            {
                OnUnbindCalled = true;
            }
        }
    }
}
