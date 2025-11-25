using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Strada.Core.Communication
{
    public interface IEventData { }

    public sealed class ZeroAllocEventBus : IDisposable
    {
        private readonly Dictionary<Type, object> _handlers = new(32);
        private readonly Dictionary<Type, object> _eventPools = new(32);
        private bool _disposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Subscribe<T>(Action<T> handler) where T : struct, IEventData
        {
            var type = typeof(T);

            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Action<T>>(8);
                _handlers[type] = list;
            }

            ((List<Action<T>>)list).Add(handler);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unsubscribe<T>(Action<T> handler) where T : struct, IEventData
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
            {
                var typedList = (List<Action<T>>)list;
                for (int i = typedList.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(typedList[i], handler))
                    {
                        typedList.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<T>(T evt) where T : struct, IEventData
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                return;

            var handlers = (List<Action<T>>)list;
            int count = handlers.Count;

            for (int i = 0; i < count; i++)
            {
                handlers[i](evt);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<T>(ref T evt) where T : struct, IEventData
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                return;

            var handlers = (List<Action<T>>)list;
            int count = handlers.Count;

            for (int i = 0; i < count; i++)
            {
                handlers[i](evt);
            }
        }

        public int GetSubscriberCount<T>() where T : struct, IEventData
        {
            return _handlers.TryGetValue(typeof(T), out var list)
                ? ((List<Action<T>>)list).Count
                : 0;
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
    }

    public sealed class TypedEventChannel<T> where T : struct, IEventData
    {
        private readonly List<Action<T>> _handlers = new(8);
        private readonly List<Action<T>> _pendingAdd = new(4);
        private readonly List<Action<T>> _pendingRemove = new(4);
        private bool _dispatching;

        public int ListenerCount => _handlers.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Subscribe(Action<T> handler)
        {
            if (_dispatching)
            {
                _pendingAdd.Add(handler);
                return;
            }
            _handlers.Add(handler);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unsubscribe(Action<T> handler)
        {
            if (_dispatching)
            {
                _pendingRemove.Add(handler);
                return;
            }

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
        public void Publish(T evt)
        {
            _dispatching = true;

            int count = _handlers.Count;
            for (int i = 0; i < count; i++)
            {
                _handlers[i](evt);
            }

            _dispatching = false;
            ProcessPending();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(ref T evt)
        {
            _dispatching = true;

            int count = _handlers.Count;
            for (int i = 0; i < count; i++)
            {
                _handlers[i](evt);
            }

            _dispatching = false;
            ProcessPending();
        }

        private void ProcessPending()
        {
            for (int i = 0; i < _pendingAdd.Count; i++)
                _handlers.Add(_pendingAdd[i]);
            _pendingAdd.Clear();

            for (int i = 0; i < _pendingRemove.Count; i++)
            {
                var handler = _pendingRemove[i];
                for (int j = _handlers.Count - 1; j >= 0; j--)
                {
                    if (ReferenceEquals(_handlers[j], handler))
                    {
                        _handlers.RemoveAt(j);
                        break;
                    }
                }
            }
            _pendingRemove.Clear();
        }

        public void Clear()
        {
            _handlers.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }
    }
}
