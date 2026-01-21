using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Strada.Core.Sync
{
    /// <summary>
    /// Central manager for all view pools. Creates persistent GameObjects that survive scene transitions.
    /// Entities and pools live independently of Unity scenes.
    /// </summary>
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

        /// <summary>
        /// Register a new view pool for the specified view type.
        /// </summary>
        /// <param name="prefab">The prefab to instantiate for this view type</param>
        /// <param name="prewarmCount">Number of instances to create immediately</param>
        /// <param name="maxSize">Maximum pool size (default 1000)</param>
        /// <returns>The created pool</returns>
        public ViewPool<TView> RegisterPool<TView>(GameObject prefab, int prewarmCount = 0, int maxSize = 1000)
            where TView : EntityView
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            var viewType = typeof(TView);
            if (_pools.ContainsKey(viewType))
            {
                StradaLog.LogWarning($"Pool for {viewType.Name} already registered. Returning existing pool.", LogModule.Sync);
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

        /// <summary>
        /// Get the pool for a specific view type.
        /// </summary>
        public ViewPool<TView> GetPool<TView>() where TView : EntityView
        {
            return _pools.TryGetValue(typeof(TView), out var pool)
                ? pool as ViewPool<TView>
                : null;
        }

        /// <summary>
        /// Check if a pool exists for the specified view type.
        /// </summary>
        public bool HasPool<TView>() where TView : EntityView
        {
            return _pools.ContainsKey(typeof(TView));
        }

        /// <summary>
        /// Spawn a view from the pool and bind it to the entity.
        /// </summary>
        public TView Spawn<TView>(Entity entity, Transform parent = null) where TView : EntityView
        {
            var pool = GetPool<TView>();
            if (pool == null)
            {
                StradaLog.LogError($"No pool registered for {typeof(TView).Name}", LogModule.Sync);
                return null;
            }
            return pool.Spawn(entity, parent);
        }

        /// <summary>
        /// Spawn a view from the pool at a specific position and rotation.
        /// </summary>
        public TView Spawn<TView>(Entity entity, Vector3 position, Quaternion rotation, Transform parent = null) where TView : EntityView
        {
            var pool = GetPool<TView>();
            if (pool == null)
            {
                StradaLog.LogError($"No pool registered for {typeof(TView).Name}", LogModule.Sync);
                return null;
            }
            return pool.Spawn(entity, position, rotation, parent);
        }

        /// <summary>
        /// Despawn a view and return it to the pool.
        /// </summary>
        public void Despawn<TView>(TView view) where TView : EntityView
        {
            var pool = GetPool<TView>();
            pool?.Despawn(view);
        }

        /// <summary>
        /// Despawn a view by its bound entity.
        /// </summary>
        public void DespawnByEntity<TView>(Entity entity) where TView : EntityView
        {
            var pool = GetPool<TView>();
            pool?.Despawn(entity);
        }

        /// <summary>
        /// Clear all pools but keep them registered.
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
        }

        /// <summary>
        /// Despawn all active views but keep pools intact.
        /// </summary>
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
