using System;

namespace Strada.Core.MVCS
{
    public abstract class StradaController : IController
    {
        protected bool IsInitialized { get; private set; }

        public virtual void Initialize()
        {
            IsInitialized = true;
        }

        public virtual void Update(float deltaTime) { }
    }

    public abstract class StradaController<TModel> : StradaController where TModel : IModel
    {
        protected TModel Model { get; private set; }

        protected StradaController(TModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }
    }

    public abstract class DisposableController : StradaController, IDisposableController
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            OnDispose();
            GC.SuppressFinalize(this);
        }

        protected virtual void OnDispose() { }
    }

    public abstract class FixedUpdateController : StradaController, IFixedUpdateController
    {
        public virtual void FixedUpdate(float fixedDeltaTime) { }
    }
}
