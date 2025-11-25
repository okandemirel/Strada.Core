using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.Events
{
    public interface IEvent { }

    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : struct, IEvent;
        void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent;
        void Publish<T>(T evt) where T : struct, IEvent;
        void Clear();
    }

    public sealed class EventBus : IEventBus, IDisposable
    {
        private readonly Dictionary<Type, object> _handlers = new();
        private readonly Queue<(Type type, object evt)> _pendingEvents = new();
        private bool _dispatching;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Subscribe<T>(Action<T> handler) where T : struct, IEvent
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
        public void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                ((List<Action<T>>)list).Remove(handler);
        }

        public void Publish<T>(T evt) where T : struct, IEvent
        {
            if (_dispatching)
            {
                _pendingEvents.Enqueue((typeof(T), evt));
                return;
            }

            DispatchEvent(evt);
            ProcessPendingEvents();
        }

        public void Clear()
        {
            _handlers.Clear();
            _pendingEvents.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        private void DispatchEvent<T>(T evt) where T : struct, IEvent
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                return;

            _dispatching = true;

            var handlers = (List<Action<T>>)list;
            for (int i = 0; i < handlers.Count; i++)
                handlers[i](evt);

            _dispatching = false;
        }

        private void ProcessPendingEvents()
        {
            while (_pendingEvents.Count > 0)
            {
                var (type, evt) = _pendingEvents.Dequeue();

                if (!_handlers.TryGetValue(type, out var list))
                    continue;

                _dispatching = true;

                var method = list.GetType().GetMethod("get_Item");
                var count = (int)list.GetType().GetProperty("Count").GetValue(list);

                for (int i = 0; i < count; i++)
                {
                    var handler = method.Invoke(list, new object[] { i });
                    ((Delegate)handler).DynamicInvoke(evt);
                }

                _dispatching = false;
            }
        }
    }
}
