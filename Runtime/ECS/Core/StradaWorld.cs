using System;
using System.Reflection;

namespace Strada.Core.ECS
{
    public interface IStradaWorld : IDisposable
    {
        string Name { get; }
        bool IsInitialized { get; }
        bool IsDisposed { get; }
        IEntityManager EntityManager { get; }
        int SystemCount { get; }

        void RegisterSystem<T>() where T : struct, IStradaSystem;
        void RegisterSystem(Type systemType);
        void Initialize();
        void Update(float deltaTime);
        T GetSystem<T>() where T : struct, IStradaSystem;
        bool HasSystem<T>() where T : struct, IStradaSystem;
        void SetSystemEnabled<T>(bool enabled) where T : struct, IStradaSystem;
    }

    public class StradaWorld : IStradaWorld
    {
        private readonly SystemRegistry _systemRegistry;
        private readonly EntityManager _entityManager;
        private bool _isInitialized;
        private bool _isDisposed;
        private double _totalTime;

        public string Name { get; }
        public bool IsInitialized => _isInitialized;
        public bool IsDisposed => _isDisposed;
        public IEntityManager EntityManager => _entityManager;
        public int SystemCount => _systemRegistry.Count;

        private StradaWorld(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("World name cannot be null or empty", nameof(name));

            Name = name;
            _systemRegistry = new SystemRegistry();
            _entityManager = new EntityManager();
            _isInitialized = false;
            _isDisposed = false;
            _totalTime = 0.0;
        }

        public static IStradaWorld Create(string name)
        {
            return new StradaWorld(name);
        }

        public static IStradaWorld CreateWithAutoDiscovery(string name, Func<Assembly, bool> assemblyFilter = null)
        {
            var world = new StradaWorld(name);
            world.DiscoverAndRegisterSystems(assemblyFilter);
            return world;
        }

        public void DiscoverAndRegisterSystems(Func<Assembly, bool> assemblyFilter = null)
        {
            if (_isInitialized)
                throw new InvalidOperationException("Cannot discover systems after world is initialized");

            _systemRegistry.DiscoverAndRegister(assemblyFilter);
        }

        public void RegisterSystem<T>() where T : struct, IStradaSystem
        {
            RegisterSystem(typeof(T));
        }

        public void RegisterSystem(Type systemType)
        {
            if (_isInitialized)
                throw new InvalidOperationException("Cannot register systems after world is initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(StradaWorld));

            _systemRegistry.Register(systemType);
        }

        public void Initialize()
        {
            if (_isInitialized)
                throw new InvalidOperationException("World is already initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(StradaWorld));

            try
            {
                _systemRegistry.Initialize(() => CreateSystemState());
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize world: {ex.Message}", ex);
            }
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("World is not initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(StradaWorld));

            _totalTime += deltaTime;

            try
            {
                _systemRegistry.Update(() => CreateSystemState(deltaTime));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error updating world {Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public T GetSystem<T>() where T : struct, IStradaSystem
        {
            if (!_isInitialized)
                throw new InvalidOperationException("World is not initialized");

            return _systemRegistry.GetSystem<T>();
        }

        public bool HasSystem<T>() where T : struct, IStradaSystem
        {
            return _systemRegistry.HasSystem<T>();
        }

        public void SetSystemEnabled<T>(bool enabled) where T : struct, IStradaSystem
        {
            _systemRegistry.SetSystemEnabled<T>(enabled);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                _systemRegistry.Destroy(() => CreateSystemState());
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error disposing world {Name}: {ex.Message}");
            }

            _entityManager.Dispose();
            _isDisposed = true;
        }

        private SystemState CreateSystemState(float deltaTime = 0f)
        {
            return new SystemState
            {
                EntityManager = _entityManager,
                DeltaTime = deltaTime,
                Time = _totalTime,
                Enabled = true
            };
        }
    }
}
