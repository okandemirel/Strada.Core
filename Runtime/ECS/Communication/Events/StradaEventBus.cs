using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

namespace Strada.Core.ECS.Communication
{
    public class StradaEventBus : IEventPublisher, IEventSubscriber, IEventDispatcher
    {
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<object>> _eventQueues;
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
            _eventQueues = new ConcurrentDictionary<Type, ConcurrentQueue<object>>();
            _subscribers = new Dictionary<Type, List<Delegate>>();
        }

        public void Raise<T>(T eventData) where T : struct, IStradaEvent
        {
            var type = typeof(T);
            var queue = _eventQueues.GetOrAdd(type, _ => new ConcurrentQueue<object>());
            queue.Enqueue(eventData);
        }

        public void Clear()
        {
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
                    while (queue.TryDequeue(out _)) { }
                    return;
                }

                handlers = new List<Delegate>(handlers);
            }

            while (queue.TryDequeue(out var eventObj))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler.DynamicInvoke(eventObj);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error dispatching event {eventType.Name}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }
    }
}
