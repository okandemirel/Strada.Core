using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Strada.Core.Sync
{
    public class PoolManager : IDisposable
    {
        private readonly Dictionary<Type, IViewPool> _pools = new();
        private readonly ViewRegistry _viewRegistry;
        private readonly EntityManager _entityManager;
        private readonly IContainer _container;

        private Transform _rootTransform;
        private Transform _poolRoot;
        private Transform _activeRoot;
        private ViewSyncRunner _syncRunner;
        private bool _disposed;

        public ViewRegistry Registry => _viewRegistry;
        public int PoolCount => _pools.Count;

        public PoolManager(EntityManager entityManager, IContainer container)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _viewRegistry = new ViewRegistry(entityManager, container);

            CreatePersistentRoots();
            CreateSyncRunner();
        }

        private void CreatePersistentRoots()
        {
            var rootGO = new GameObject("[PoolManager]");
            Object.DontDestroyOnLoad(rootGO);
            _rootTransform = rootGO.transform;

            _poolRoot = new GameObject("PooledViews").transform;
            _poolRoot.SetParent(_rootTransform);
            _poolRoot.gameObject.SetActive(false);

            _activeRoot = new GameObject("ActiveViews").transform;
            _activeRoot.SetParent(_rootTransform);
        }

        private void CreateSyncRunner()
        {
            var syncGO = new GameObject("ViewSyncRunner");
            syncGO.transform.SetParent(_rootTransform);
            _syncRunner = syncGO.AddComponent<ViewSyncRunner>();
            _syncRunner.Initialize(_viewRegistry);
        }

        public ViewPool<TView> RegisterPool<TView>(GameObject prefab, int prewarmCount = 0, int maxSize = 1000)
            where TView : EntityView
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            var viewType = typeof(TView);
            if (_pools.ContainsKey(viewType))
            {
                Debug.LogWarning($"[PoolManager] Pool for {viewType.Name} already registered. Returning existing pool.");
                return _pools[viewType] as ViewPool<TView>;
            }

            var typePoolRoot = new GameObject($"Pool_{viewType.Name}").transform;
            typePoolRoot.SetParent(_poolRoot);

            var typeActiveRoot = new GameObject($"Active_{viewType.Name}").transform;
            typeActiveRoot.SetParent(_activeRoot);

            var pool = new ViewPool<TView>(
                prefab,
                _container,
                _entityManager,
                _viewRegistry,
                typePoolRoot,
                typeActiveRoot,
                prewarmCount,
                maxSize);

            _pools[viewType] = pool;
            return pool;
        }

        public ViewPool<TView> GetPool<TView>() where TView : EntityView
        {
            return _pools.TryGetValue(typeof(TView), out var pool)
                ? pool as ViewPool<TView>
                : null;
        }

        public bool HasPool<TView>() where TView : EntityView
        {
            return _pools.ContainsKey(typeof(TView));
        }

        public TView Spawn<TView>(Entity entity, Transform parent = null) where TView : EntityView
        {
            var pool = GetPool<TView>();
            if (pool == null)
            {
                Debug.LogError($"[PoolManager] No pool registered for {typeof(TView).Name}");
                return null;
            }
            return pool.Spawn(entity, parent);
        }

        public TView Spawn<TView>(Entity entity, Vector3 position, Quaternion rotation, Transform parent = null) where TView : EntityView
        {
            var pool = GetPool<TView>();
            if (pool == null)
            {
                Debug.LogError($"[PoolManager] No pool registered for {typeof(TView).Name}");
                return null;
            }
            return pool.Spawn(entity, position, rotation, parent);
        }

        public void Despawn<TView>(TView view) where TView : EntityView
        {
            var pool = GetPool<TView>();
            pool?.Despawn(view);
        }

        public void DespawnByEntity<TView>(Entity entity) where TView : EntityView
        {
            var pool = GetPool<TView>();
            pool?.Despawn(entity);
        }

        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
        }

        public void DespawnAllViews()
        {
            foreach (var pool in _pools.Values)
            {
                pool.DespawnAll();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            foreach (var pool in _pools.Values)
            {
                if (pool is IDisposable disposable)
                    disposable.Dispose();
            }
            _pools.Clear();

            _viewRegistry.Dispose();

            if (_rootTransform != null)
                Object.Destroy(_rootTransform.gameObject);
        }
    }
}
