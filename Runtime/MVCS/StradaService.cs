using System;

namespace Strada.Core.MVCS
{
    public abstract class StradaService : IService
    {
        protected bool IsInitialized { get; private set; }

        public virtual void Initialize()
        {
            IsInitialized = true;
        }

        public virtual void Update(float deltaTime) { }
    }

    public abstract class DisposableService : StradaService, IDisposableService
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

    public abstract class OrderedService : StradaService, IOrderedService
    {
        public abstract int InitializationOrder { get; }
    }
}
