using NUnit.Framework;
using Strada.Core.Sync;
using Strada.Core.DI;
using Strada.Core.ECS.Core;
using UnityEngine;

namespace Strada.Core.Tests.Tests.Runtime.Sync
{
    [TestFixture]
    public class ViewPoolTests
    {
        private EntityManager _entityManager;
        private IContainer _container;
        private ViewRegistry _registry;
        private GameObject _prefab;
        private Transform _poolRoot;
        private Transform _activeRoot;

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
            var builder = new ContainerBuilder();
            _container = builder.Build();
            _registry = new ViewRegistry(_entityManager, _container);

            _prefab = new GameObject("TestViewPrefab");
            _prefab.AddComponent<TestPooledView>();

            _poolRoot = new GameObject("PoolRoot").transform;
            _activeRoot = new GameObject("ActiveRoot").transform;
        }

        [TearDown]
        public void TearDown()
        {
            _entityManager?.Dispose();
            _container?.Dispose();
            _registry?.Dispose();

            if (_prefab != null) Object.DestroyImmediate(_prefab);
            if (_poolRoot != null) Object.DestroyImmediate(_poolRoot.gameObject);
            if (_activeRoot != null) Object.DestroyImmediate(_activeRoot.gameObject);
        }

        [Test]
        public void ViewPool_Spawn_CreatesView()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity = _entityManager.CreateEntity();
            var view = pool.Spawn(entity);

            Assert.IsNotNull(view);
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.AreEqual(1, pool.TotalCreated);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_Spawn_BindsEntity()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity = _entityManager.CreateEntity();
            var view = pool.Spawn(entity);

            Assert.AreEqual(entity.Index, view.Entity.Index);
            Assert.IsTrue(view.IsBound);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_Despawn_ReturnsToPool()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity = _entityManager.CreateEntity();
            var view = pool.Spawn(entity);
            pool.Despawn(view);

            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(1, pool.AvailableCount);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_Despawn_UnbindsEntity()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity = _entityManager.CreateEntity();
            var view = pool.Spawn(entity);
            pool.Despawn(view);

            Assert.IsFalse(view.IsBound);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_RespawnFromPool_ReusesInstance()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity1 = _entityManager.CreateEntity();
            var view1 = pool.Spawn(entity1);
            pool.Despawn(view1);

            var entity2 = _entityManager.CreateEntity();
            var view2 = pool.Spawn(entity2);

            Assert.AreSame(view1, view2);
            Assert.AreEqual(1, pool.TotalCreated);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_Prewarm_PreallocatesViews()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot, initialSize: 5);

            Assert.AreEqual(5, pool.AvailableCount);
            Assert.AreEqual(5, pool.TotalCreated);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_DespawnAll_ReturnsAllToPool()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            for (int i = 0; i < 5; i++)
            {
                var entity = _entityManager.CreateEntity();
                pool.Spawn(entity);
            }

            pool.DespawnAll();

            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(5, pool.AvailableCount);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_Clear_DestroysAllViews()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot, initialSize: 3);

            pool.Clear();

            Assert.AreEqual(0, pool.AvailableCount);
            Assert.AreEqual(0, pool.TotalCreated);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_SpawnWithPosition_SetsTransform()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity = _entityManager.CreateEntity();
            var position = new Vector3(1, 2, 3);
            var rotation = Quaternion.Euler(45, 90, 0);

            var view = pool.Spawn(entity, position, rotation);

            Assert.AreEqual(position, view.transform.position);
            Assert.AreEqual(rotation.eulerAngles, view.transform.rotation.eulerAngles);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_DespawnByEntity_FindsAndDespawns()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity = _entityManager.CreateEntity();
            pool.Spawn(entity);

            pool.Despawn(entity);

            Assert.AreEqual(0, pool.ActiveCount);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_RegistersWithRegistry()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity = _entityManager.CreateEntity();
            var view = pool.Spawn(entity);

            var registeredView = _registry.GetView(entity);

            Assert.AreSame(view, registeredView);

            pool.Dispose();
        }

        [Test]
        public void ViewPool_Despawn_UnregistersFromRegistry()
        {
            var pool = new ViewPool<TestPooledView>(
                _prefab, _container, _entityManager, _registry,
                _poolRoot, _activeRoot);

            var entity = _entityManager.CreateEntity();
            var view = pool.Spawn(entity);
            pool.Despawn(view);

            var registeredView = _registry.GetView(entity);

            Assert.IsNull(registeredView);

            pool.Dispose();
        }

        private class TestPooledView : EntityView
        {
            protected override void OnBind() { }
            protected override void OnUnbind() { }
        }
    }
}
