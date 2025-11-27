using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.Bridge;
using Strada.Core.MVCS.Interfaces;

namespace Strada.Core.MVCS
{
    public abstract class Model : IModel, IInitializable, IDisposable
    {
        private readonly List<IDisposable> _disposables = new(4);
        private bool _initialized;
        private bool _disposed;

        protected bool IsInitialized => _initialized;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnDispose() { }

        public virtual bool Validate() => _initialized;

        protected ReactiveProperty<T> CreateProperty<T>(T initialValue = default)
        {
            var property = new ReactiveProperty<T>(initialValue);
            _disposables.Add(property);
            return property;
        }

        protected ReactiveCollection<T> CreateCollection<T>()
        {
            var collection = new ReactiveCollection<T>();
            _disposables.Add(collection);
            return collection;
        }

        protected void AddDisposable(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            OnDispose();

            foreach (var disposable in _disposables)
                disposable.Dispose();
            _disposables.Clear();

            GC.SuppressFinalize(this);
        }
    }

    public abstract class Model<TData> : Model where TData : class, new()
    {
        private ReactiveProperty<TData> _dataProperty;

        protected TData Data => _dataProperty?.Value;

        protected IReadOnlyReactiveProperty<TData> DataProperty => _dataProperty;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _dataProperty = CreateProperty(new TData());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetData(TData data)
        {
            _dataProperty.Value = data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdateData(Action<TData> updater)
        {
            updater(Data);
            _dataProperty.Notify();
        }

        public override bool Validate() => base.Validate() && Data != null;
    }

    public abstract class ReactiveModel : Model
    {
        private readonly Dictionary<string, object> _properties = new(8);

        protected ReactiveProperty<T> Property<T>(string name, T initialValue = default)
        {
            if (_properties.TryGetValue(name, out var existing))
                return (ReactiveProperty<T>)existing;

            var property = CreateProperty(initialValue);
            _properties[name] = property;
            return property;
        }

        protected IReadOnlyReactiveProperty<T> GetProperty<T>(string name)
        {
            return _properties.TryGetValue(name, out var property)
                ? (IReadOnlyReactiveProperty<T>)property
                : null;
        }
    }
}
