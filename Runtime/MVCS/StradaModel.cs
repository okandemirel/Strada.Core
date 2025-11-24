using System;

namespace Strada.Core.MVCS
{
    public abstract class StradaModel : IModel
    {
        protected bool IsInitialized { get; private set; }

        public virtual void Initialize()
        {
            IsInitialized = true;
        }

        public virtual bool Validate()
        {
            return IsInitialized;
        }
    }

    public abstract class StradaModel<TData> : StradaModel where TData : class
    {
        protected TData Data { get; private set; }

        protected void SetData(TData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Data = data;
        }

        public override bool Validate()
        {
            return base.Validate() && Data != null;
        }
    }

    public abstract class DisposableModel : StradaModel, IDisposableModel
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
}
