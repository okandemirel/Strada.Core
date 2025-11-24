using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

namespace Strada.Core.ECS.Communication
{
    internal interface IEventQueue
    {
        int Count { get; }
        void Clear();
        void DispatchTo(List<Delegate> handlers);
    }

    internal sealed class EventQueue<T> : IEventQueue where T : struct, IStradaEvent
    {
        private readonly ConcurrentQueue<T> _queue;

        public int Count
        {
            get
            {
                int count = 0;
                foreach (var _ in _queue) count++;
                return count;
            }
        }

        public EventQueue()
        {
            _queue = new ConcurrentQueue<T>();
        }

        public void Enqueue(T eventData)
        {
            _queue.Enqueue(eventData);
        }

        public void Clear()
        {
            while (_queue.TryDequeue(out _)) { }
        }

        public void DispatchTo(List<Delegate> handlers)
        {
            while (_queue.TryDequeue(out var eventData))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        ((Action<T>)handler)(eventData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error dispatching event {typeof(T).Name}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }
    }

    public class StradaEventBus : IEventPublisher, IEventSubscriber, IEventDispatcher
    {
        private readonly ConcurrentDictionary<Type, IEventQueue> _eventQueues;
        private readonly Dictionary<Type, List<Delegate>> _subscribers;
        private readonly object _lockObject = new object();

        public int PendingCount
        {
            get
            {
                int count = 0;
                foreach (var queue in _eventQueues.Values)
                {
                    count += queue.Count;
                }
                return count;
            }
        }

        public StradaEventBus()
        {
            _eventQueues = new ConcurrentDictionary<Type, IEventQueue>();
            _subscribers = new Dictionary<Type, List<Delegate>>();
        }

        public void Raise<T>(T eventData) where T : struct, IStradaEvent
        {
            var type = typeof(T);
            var queue = (EventQueue<T>)_eventQueues.GetOrAdd(type, _ => new EventQueue<T>());
            queue.Enqueue(eventData);
        }

        public void Clear()
        {
            foreach (var queue in _eventQueues.Values)
            {
                queue.Clear();
            }
            _eventQueues.Clear();
        }

        public void Subscribe<T>(Action<T> handler) where T : struct, IStradaEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var type = typeof(T);
            lock (_lockObject)
            {
                if (!_subscribers.TryGetValue(type, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _subscribers[type] = handlers;
                }

                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                }
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, IStradaEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var type = typeof(T);
            lock (_lockObject)
            {
                if (_subscribers.TryGetValue(type, out var handlers))
                {
                    handlers.Remove(handler);

                    if (handlers.Count == 0)
                    {
                        _subscribers.Remove(type);
                    }
                }
            }
        }

        public void UnsubscribeAll<T>() where T : struct, IStradaEvent
        {
            var type = typeof(T);
            lock (_lockObject)
            {
                _subscribers.Remove(type);
            }
        }

        public bool HasSubscribers<T>() where T : struct, IStradaEvent
        {
            return GetSubscriberCount<T>() > 0;
        }

        public int GetSubscriberCount<T>() where T : struct, IStradaEvent
        {
            var type = typeof(T);
            lock (_lockObject)
            {
                if (_subscribers.TryGetValue(type, out var handlers))
                {
                    return handlers.Count;
                }
            }
            return 0;
        }

        public void DispatchPendingEvents()
        {
            var eventTypes = new List<Type>(_eventQueues.Keys);

            foreach (var eventType in eventTypes)
            {
                DispatchEventsOfType(eventType);
            }
        }

        public void DispatchEvents<T>() where T : struct, IStradaEvent
        {
            DispatchEventsOfType(typeof(T));
        }

        private void DispatchEventsOfType(Type eventType)
        {
            if (!_eventQueues.TryGetValue(eventType, out var queue))
                return;

            List<Delegate> handlers;
            lock (_lockObject)
            {
                if (!_subscribers.TryGetValue(eventType, out handlers) || handlers.Count == 0)
                {
                    queue.Clear();
                    return;
                }

                handlers = new List<Delegate>(handlers);
            }

            queue.DispatchTo(handlers);
        }
    }
}
