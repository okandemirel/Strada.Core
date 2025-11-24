using System;
using System.Collections.Generic;
using System.Linq;

namespace Strada.Core.MVCS
{
    public sealed class MVCSManager : IDisposable
    {
        private readonly List<IController> _controllers = new();
        private readonly List<IService> _services = new();
        private readonly List<IFixedUpdateController> _fixedControllers = new();
        private bool _disposed;

        public int ControllerCount => _controllers.Count;
        public int ServiceCount => _services.Count;

        public void RegisterController(IController controller)
        {
            _controllers.Add(controller);

            if (controller is IFixedUpdateController fixedController)
                _fixedControllers.Add(fixedController);
        }

        public void RegisterService(IService service)
        {
            _services.Add(service);
        }

        public void Initialize()
        {
            var orderedServices = _services
                .OrderBy(s => s is IOrderedService ordered ? ordered.InitializationOrder : int.MaxValue);

            foreach (var service in orderedServices)
                service.Initialize();

            foreach (var controller in _controllers)
                controller.Initialize();
        }

        public void Update(float deltaTime)
        {
            for (int i = 0; i < _controllers.Count; i++)
                _controllers[i].Update(deltaTime);

            for (int i = 0; i < _services.Count; i++)
                _services[i].Update(deltaTime);
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            for (int i = 0; i < _fixedControllers.Count; i++)
                _fixedControllers[i].FixedUpdate(fixedDeltaTime);
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
        }
    }
}
