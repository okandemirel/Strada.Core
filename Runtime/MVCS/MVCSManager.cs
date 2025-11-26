using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.MVCS.Interfaces;

namespace Strada.Core.MVCS
{
    public sealed class MVCSManager : IDisposable, ILoopRunner
    {
        private readonly List<IController> _controllers = new();
        private readonly List<IService> _services = new();
        private readonly List<IFixedTickController> _fixedControllers = new();
        private readonly List<ITickable> _tickables = new();
        private readonly List<IFixedTickable> _fixedTickables = new();
        private readonly List<ILateTickable> _lateTickables = new();
        private bool _disposed;
        private bool _registeredWithLoop;

        public int ControllerCount => _controllers.Count;
        public int ServiceCount => _services.Count;

        public void RegisterController(IController controller)
        {
            _controllers.Add(controller);

            if (controller is IFixedTickController fixedController)
                _fixedControllers.Add(fixedController);

            if (controller is ITickable tickable)
                _tickables.Add(tickable);

            if (controller is IFixedTickable fixedTickable)
                _fixedTickables.Add(fixedTickable);

            if (controller is ILateTickable lateTickable)
                _lateTickables.Add(lateTickable);
        }

        public void RegisterService(IService service)
        {
            _services.Add(service);

            if (service is ITickable tickable)
                _tickables.Add(tickable);

            if (service is IFixedTickable fixedTickable)
                _fixedTickables.Add(fixedTickable);

            if (service is ILateTickable lateTickable)
                _lateTickables.Add(lateTickable);
        }

        public void Initialize()
        {
            var orderedServices = _services
                .OrderBy(s => s is IOrderedService ordered ? ordered.InitializationOrder : int.MaxValue);

            foreach (var service in orderedServices)
                service.Initialize();

            foreach (var controller in _controllers)
                controller.Initialize();

            RegisterWithPlayerLoop();
        }

        public void RegisterWithPlayerLoop()
        {
            if (_registeredWithLoop) return;
            _registeredWithLoop = true;

            StradaPlayerLoop.RegisterUpdate(OnUpdate);
            StradaPlayerLoop.RegisterLateUpdate(OnLateUpdate);
            StradaPlayerLoop.RegisterFixedUpdate(OnFixedUpdate);
        }

        public void UnregisterFromPlayerLoop()
        {
            if (!_registeredWithLoop) return;
            _registeredWithLoop = false;

            StradaPlayerLoop.UnregisterUpdate(OnUpdate);
            StradaPlayerLoop.UnregisterLateUpdate(OnLateUpdate);
            StradaPlayerLoop.UnregisterFixedUpdate(OnFixedUpdate);
        }

        public void Update(float deltaTime) => OnUpdate(deltaTime);
        public void FixedUpdate(float fixedDeltaTime) => OnFixedUpdate(fixedDeltaTime);
        public void LateUpdate(float deltaTime) => OnLateUpdate(deltaTime);

        public void OnUpdate(float deltaTime)
        {
            for (int i = 0; i < _controllers.Count; i++)
                _controllers[i].Tick(deltaTime);

            for (int i = 0; i < _services.Count; i++)
                _services[i].Tick(deltaTime);

            for (int i = 0; i < _tickables.Count; i++)
                _tickables[i].Tick(deltaTime);
        }

        public void OnFixedUpdate(float fixedDeltaTime)
        {
            for (int i = 0; i < _fixedControllers.Count; i++)
                _fixedControllers[i].FixedTick(fixedDeltaTime);

            for (int i = 0; i < _fixedTickables.Count; i++)
                _fixedTickables[i].FixedTick(fixedDeltaTime);
        }

        public void OnLateUpdate(float deltaTime)
        {
            for (int i = 0; i < _lateTickables.Count; i++)
                _lateTickables[i].LateTick(deltaTime);
        }

        public T GetService<T>() where T : class, IService
        {
            return _services.OfType<T>().FirstOrDefault();
        }

        public T GetController<T>() where T : class, IController
        {
            return _controllers.OfType<T>().FirstOrDefault();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            UnregisterFromPlayerLoop();

            for (int i = _controllers.Count - 1; i >= 0; i--)
            {
                if (_controllers[i] is IDisposable disposable)
                    disposable.Dispose();
            }

            for (int i = _services.Count - 1; i >= 0; i--)
            {
                if (_services[i] is IDisposable disposable)
                    disposable.Dispose();
            }

            _controllers.Clear();
            _services.Clear();
            _fixedControllers.Clear();
            _tickables.Clear();
            _fixedTickables.Clear();
            _lateTickables.Clear();
        }
    }
}
