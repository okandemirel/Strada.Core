using System;
using Strada.Core.MVCS.Interfaces;
using UnityEngine;

namespace Strada.Core.MVCS
{
    public abstract class StradaView : MonoBehaviour, IView
    {
        public bool IsVisible { get; private set; }

        public virtual void Initialize()
        {
            IsVisible = gameObject.activeSelf;
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            IsVisible = true;
            OnShow();
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
            IsVisible = false;
            OnHide();
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }

    public abstract class StradaView<TModel> : StradaView, IView<TModel> where TModel : IModel
    {
        protected TModel Model { get; private set; }

        public void SetModel(TModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            OnModelSet();
        }

        public virtual void UpdateView(TModel model)
        {
            Model = model;
            OnViewUpdate();
        }

        protected virtual void OnModelSet() { }
        protected virtual void OnViewUpdate() { }
    }

    public abstract class DisposableView : StradaView, IDisposableView
    {
        private bool _disposed;

        protected virtual void OnDestroy()
        {
            Dispose();
        }

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
