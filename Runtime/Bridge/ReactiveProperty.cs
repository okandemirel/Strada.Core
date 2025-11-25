using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.Bridge
{
    public interface IReadOnlyReactiveProperty<T>
    {
        T Value { get; }
        void Subscribe(Action<T> handler);
        void Unsubscribe(Action<T> handler);
    }

    public sealed class ReactiveProperty<T> : IReadOnlyReactiveProperty<T>, IDisposable
    {
        private T _value;
        private readonly List<Action<T>> _handlers = new(4);
        private readonly EqualityComparer<T> _comparer = EqualityComparer<T>.Default;
        private bool _disposed;

        public ReactiveProperty() => _value = default;

        public ReactiveProperty(T initialValue) => _value = initialValue;

        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (_comparer.Equals(_value, value))
                    return;

                _value = value;
                Notify();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWithoutNotify(T value)
        {
            _value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Subscribe(Action<T> handler)
        {
            _handlers.Add(handler);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SubscribeAndInvoke(Action<T> handler)
        {
            _handlers.Add(handler);
            handler(_value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unsubscribe(Action<T> handler)
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_handlers[i], handler))
                {
                    _handlers.RemoveAt(i);
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Notify()
        {
            int count = _handlers.Count;
            for (int i = 0; i < count; i++)
            {
                _handlers[i](_value);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }

        public static implicit operator T(ReactiveProperty<T> property) => property.Value;
    }

    public sealed class ReactiveCollection<T> : IDisposable
    {
        private readonly List<T> _items = new();
        private readonly List<Action<T>> _addHandlers = new(4);
        private readonly List<Action<T>> _removeHandlers = new(4);
        private readonly List<Action> _clearHandlers = new(2);
        private bool _disposed;

        public int Count => _items.Count;
        public T this[int index] => _items[index];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            _items.Add(item);
            NotifyAdd(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T item)
        {
            if (_items.Remove(item))
            {
                NotifyRemove(item);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            var item = _items[index];
            _items.RemoveAt(index);
            NotifyRemove(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _items.Clear();
            NotifyClear();
        }

        public void OnAdd(Action<T> handler) => _addHandlers.Add(handler);
        public void OnRemove(Action<T> handler) => _removeHandlers.Add(handler);
        public void OnClear(Action handler) => _clearHandlers.Add(handler);

        private void NotifyAdd(T item)
        {
            for (int i = 0; i < _addHandlers.Count; i++)
                _addHandlers[i](item);
        }

        private void NotifyRemove(T item)
        {
            for (int i = 0; i < _removeHandlers.Count; i++)
                _removeHandlers[i](item);
        }

        private void NotifyClear()
        {
            for (int i = 0; i < _clearHandlers.Count; i++)
                _clearHandlers[i]();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _items.Clear();
            _addHandlers.Clear();
            _removeHandlers.Clear();
            _clearHandlers.Clear();
        }
    }
}
