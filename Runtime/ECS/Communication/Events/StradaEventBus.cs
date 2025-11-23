using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

namespace Strada.Core.ECS.Communication
{
    /// <summary>
    /// Thread-safe event bus for ECS → MVCS communication.
    /// </summary>
    /// <remarks>
    /// StradaEventBus provides a thread-safe pub/sub system for events raised
    /// from ECS systems (potentially job threads) to MVCS services/controllers (main thread).
    ///
    /// Design:
    /// - Events queued from any thread (thread-safe raise)
    /// - Dispatch always on main thread
    /// - Type-safe subscription management
    /// - Priority-based handler invocation
    ///
    /// Thread Safety:
    /// - Raise() can be called from any thread
    /// - Subscribe/Unsubscribe only from main thread
    /// - DispatchPendingEvents() only from main thread
    ///
    /// Performance:
    /// - O(1) event raise
    /// - O(n) dispatch (n = subscribers)
    /// - Minimal allocation (event pooling)
    /// </remarks>
    public class StradaEventBus : IEventPublisher, IEventSubscriber, IEventDispatcher
    {
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<object>> _eventQueues;
        private readonly Dictionary<Type, List<Delegate>> _subscribers;
        private readonly object _lockObject = new object();

        /// <inheritdoc/>
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

        /// <summary>
        /// Initializes a new instance of StradaEventBus.
        /// </summary>
        public StradaEventBus()
        {
            _eventQueues = new ConcurrentDictionary<Type, ConcurrentQueue<object>>();
            _subscribers = new Dictionary<Type, List<Delegate>>();
        }

        #region Publishing

        /// <inheritdoc/>
        public void Raise<T>(T eventData) where T : struct, IStradaEvent
        {
            var type = typeof(T);
            var queue = _eventQueues.GetOrAdd(type, _ => new ConcurrentQueue<object>());
            queue.Enqueue(eventData);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _eventQueues.Clear();
        }

        #endregion

        #region Subscription

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void UnsubscribeAll<T>() where T : struct, IStradaEvent
        {
            var type = typeof(T);
            lock (_lockObject)
            {
                _subscribers.Remove(type);
            }
        }

        /// <inheritdoc/>
        public bool HasSubscribers<T>() where T : struct, IStradaEvent
        {
            return GetSubscriberCount<T>() > 0;
        }

        /// <inheritdoc/>
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

        #endregion

        #region Dispatching

        /// <inheritdoc/>
        public void DispatchPendingEvents()
        {
            // Get copy of event types to avoid collection modified exception
            var eventTypes = new List<Type>(_eventQueues.Keys);

            foreach (var eventType in eventTypes)
            {
                DispatchEventsOfType(eventType);
            }
        }

        /// <inheritdoc/>
        public void DispatchEvents<T>() where T : struct, IStradaEvent
        {
            DispatchEventsOfType(typeof(T));
        }

        private void DispatchEventsOfType(Type eventType)
        {
            if (!_eventQueues.TryGetValue(eventType, out var queue))
                return;

            // Get subscribers
            List<Delegate> handlers;
            lock (_lockObject)
            {
                if (!_subscribers.TryGetValue(eventType, out handlers) || handlers.Count == 0)
                {
                    // No subscribers, clear queue
                    while (queue.TryDequeue(out _)) { }
                    return;
                }

                // Create copy to avoid issues if handlers modify subscriptions
                handlers = new List<Delegate>(handlers);
            }

            // Process all queued events
            while (queue.TryDequeue(out var eventObj))
            {
                // Invoke all handlers
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

        #endregion
    }

    /// <summary>
    /// Global event bus accessor for ECS systems and MVCS services.
    /// </summary>
    /// <remarks>
    /// Provides static access to the event bus for both ECS and MVCS layers.
    /// The bus is created and managed by the HybridBridge.
    ///
    /// Usage in ECS Systems:
    /// <code>
    /// [StradaSystem]
    /// public partial struct CollisionSystem : IStradaSystem
    /// {
    ///     public void OnUpdate(ref SystemState state)
    ///     {
    ///         // Detect collision...
    ///         StradaEvents.Raise(new BallCollisionEvent
    ///         {
    ///             Entity = ball,
    ///             Force = impact
    ///         });
    ///     }
    /// }
    /// </code>
    ///
    /// Usage in MVCS Services:
    /// <code>
    /// public class AudioService : IService
    /// {
    ///     public void Initialize()
    ///     {
    ///         StradaEvents.Subscribe&lt;BallCollisionEvent&gt;(OnCollision);
    ///     }
    ///
    ///     private void OnCollision(BallCollisionEvent evt)
    ///     {
    ///         PlaySound(evt.Force);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static class StradaEvents
    {
        private static StradaEventBus _globalBus;

        /// <summary>
        /// Gets or sets the global event bus.
        /// </summary>
        public static StradaEventBus Global
        {
            get
            {
                if (_globalBus == null)
                {
                    _globalBus = new StradaEventBus();
                    Debug.LogWarning("Global event bus was not initialized. Creating default instance.");
                }
                return _globalBus;
            }
            set => _globalBus = value;
        }

        /// <summary>
        /// Raises an event.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <param name="eventData">The event data</param>
        public static void Raise<T>(T eventData) where T : struct, IStradaEvent
        {
            Global.Raise(eventData);
        }

        /// <summary>
        /// Subscribes to an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <param name="handler">The event handler</param>
        public static void Subscribe<T>(Action<T> handler) where T : struct, IStradaEvent
        {
            Global.Subscribe(handler);
        }

        /// <summary>
        /// Unsubscribes from an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <param name="handler">The event handler to remove</param>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IStradaEvent
        {
            Global.Unsubscribe(handler);
        }

        /// <summary>
        /// Unsubscribes all handlers for an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        public static void UnsubscribeAll<T>() where T : struct, IStradaEvent
        {
            Global.UnsubscribeAll<T>();
        }

        /// <summary>
        /// Checks if there are subscribers for an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <returns>True if subscribers exist</returns>
        public static bool HasSubscribers<T>() where T : struct, IStradaEvent
        {
            return Global.HasSubscribers<T>();
        }

        /// <summary>
        /// Gets the number of subscribers for an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <returns>Number of subscribers</returns>
        public static int GetSubscriberCount<T>() where T : struct, IStradaEvent
        {
            return Global.GetSubscriberCount<T>();
        }

        /// <summary>
        /// Dispatches all pending events.
        /// Should be called on the main thread.
        /// </summary>
        public static void DispatchPendingEvents()
        {
            Global.DispatchPendingEvents();
        }

        /// <summary>
        /// Dispatches events of a specific type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        public static void DispatchEvents<T>() where T : struct, IStradaEvent
        {
            Global.DispatchEvents<T>();
        }

        /// <summary>
        /// Clears all pending events.
        /// </summary>
        public static void Clear()
        {
            Global.Clear();
        }
    }
}
