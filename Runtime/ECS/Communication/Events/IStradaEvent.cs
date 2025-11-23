using System;

namespace Strada.Core.ECS.Communication
{
    /// <summary>
    /// Marker interface for Strada events.
    /// Events represent notifications from ECS systems to MVCS layer.
    /// </summary>
    /// <remarks>
    /// Events flow from ECS layer to MVCS layer:
    ///
    /// Flow:
    /// 1. ECS system detects condition (collision, completion, etc.)
    /// 2. System raises event via StradaEvents.Raise()
    /// 3. Event queued (thread-safe)
    /// 4. Events dispatched on main thread
    /// 5. MVCS services/controllers handle event
    ///
    /// Best Practices:
    /// - Events should be structs (value types)
    /// - Keep events small and focused
    /// - Events are immutable once raised
    /// - Use Burst-compatible types only
    /// - No references to ECS internals
    ///
    /// Example:
    /// <code>
    /// // Define event
    /// public struct BallCollisionEvent : IStradaEvent
    /// {
    ///     public Entity BallEntity;
    ///     public float3 CollisionPoint;
    ///     public float ImpactForce;
    /// }
    ///
    /// // Raise from System (ECS)
    /// [StradaSystem]
    /// public partial struct CollisionSystem : IStradaSystem
    /// {
    ///     public void OnUpdate(ref SystemState state)
    ///     {
    ///         // Detect collision...
    ///         StradaEvents.Raise(new BallCollisionEvent
    ///         {
    ///             BallEntity = entity,
    ///             CollisionPoint = hitPoint,
    ///             ImpactForce = force
    ///         });
    ///     }
    /// }
    ///
    /// // Handle in Service (MVCS)
    /// public class AudioService : IService
    /// {
    ///     public void Initialize()
    ///     {
    ///         StradaEvents.Subscribe&lt;BallCollisionEvent&gt;(OnBallCollision);
    ///     }
    ///
    ///     private void OnBallCollision(BallCollisionEvent evt)
    ///     {
    ///         PlayCollisionSound(evt.ImpactForce);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public interface IStradaEvent
    {
        // Marker interface - events are defined by their data
    }

    /// <summary>
    /// Interface for event publishing.
    /// </summary>
    /// <remarks>
    /// Event publishers raise events from ECS systems to be handled by MVCS layer.
    /// </remarks>
    public interface IEventPublisher
    {
        /// <summary>
        /// Raises an event to be dispatched to subscribers.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <param name="eventData">The event data</param>
        void Raise<T>(T eventData) where T : struct, IStradaEvent;

        /// <summary>
        /// Gets the number of pending events.
        /// </summary>
        int PendingCount { get; }

        /// <summary>
        /// Clears all pending events.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Interface for event subscription.
    /// </summary>
    /// <remarks>
    /// Event subscribers register callbacks to be invoked when events are raised.
    /// </remarks>
    public interface IEventSubscriber
    {
        /// <summary>
        /// Subscribes to an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <param name="handler">The event handler callback</param>
        void Subscribe<T>(Action<T> handler) where T : struct, IStradaEvent;

        /// <summary>
        /// Unsubscribes from an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <param name="handler">The event handler to remove</param>
        void Unsubscribe<T>(Action<T> handler) where T : struct, IStradaEvent;

        /// <summary>
        /// Unsubscribes all handlers for an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        void UnsubscribeAll<T>() where T : struct, IStradaEvent;

        /// <summary>
        /// Checks if there are subscribers for an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <returns>True if subscribers exist</returns>
        bool HasSubscribers<T>() where T : struct, IStradaEvent;

        /// <summary>
        /// Gets the number of subscribers for an event type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <returns>Number of subscribers</returns>
        int GetSubscriberCount<T>() where T : struct, IStradaEvent;
    }

    /// <summary>
    /// Interface for event dispatching.
    /// </summary>
    /// <remarks>
    /// Event dispatchers process pending events and invoke subscriber callbacks.
    /// This should be called on the main thread.
    /// </remarks>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Dispatches all pending events to subscribers.
        /// </summary>
        void DispatchPendingEvents();

        /// <summary>
        /// Dispatches events of a specific type.
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        void DispatchEvents<T>() where T : struct, IStradaEvent;
    }

    /// <summary>
    /// Attribute to mark a system as an event raiser.
    /// </summary>
    /// <remarks>
    /// Systems marked with this attribute are automatically registered
    /// as raisers of specific event types.
    ///
    /// Example:
    /// <code>
    /// [StradaSystem]
    /// [EventRaiser(typeof(BallCollisionEvent))]
    /// public partial struct CollisionSystem : IStradaSystem
    /// {
    ///     // Raises BallCollisionEvent
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class EventRaiserAttribute : Attribute
    {
        /// <summary>
        /// The event types this system raises.
        /// </summary>
        public Type[] EventTypes { get; }

        /// <summary>
        /// Initializes a new instance of EventRaiserAttribute.
        /// </summary>
        /// <param name="eventTypes">The event types to raise</param>
        public EventRaiserAttribute(params Type[] eventTypes)
        {
            EventTypes = eventTypes ?? Array.Empty<Type>();

            // Validate event types
            foreach (var type in EventTypes)
            {
                if (!typeof(IStradaEvent).IsAssignableFrom(type))
                {
                    throw new ArgumentException(
                        $"Type {type.Name} must implement IStradaEvent",
                        nameof(eventTypes));
                }
            }
        }
    }

    /// <summary>
    /// Event priority for dispatch ordering.
    /// </summary>
    public enum EventPriority
    {
        /// <summary>
        /// Lowest priority (dispatched last).
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority (default).
        /// </summary>
        Normal = 50,

        /// <summary>
        /// High priority (dispatched first).
        /// </summary>
        High = 100
    }

    /// <summary>
    /// Attribute to specify event handler priority.
    /// </summary>
    /// <remarks>
    /// Higher priority handlers are invoked before lower priority handlers.
    ///
    /// Example:
    /// <code>
    /// [EventPriority(EventPriority.High)]
    /// public void OnCriticalEvent(CriticalEvent evt)
    /// {
    ///     // Handled first
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class EventPriorityAttribute : Attribute
    {
        /// <summary>
        /// The priority level.
        /// </summary>
        public EventPriority Priority { get; }

        /// <summary>
        /// Initializes a new instance of EventPriorityAttribute.
        /// </summary>
        /// <param name="priority">The priority level</param>
        public EventPriorityAttribute(EventPriority priority)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// Information about event processing.
    /// </summary>
    public struct EventProcessingInfo
    {
        /// <summary>
        /// The event type.
        /// </summary>
        public Type EventType;

        /// <summary>
        /// Number of events raised.
        /// </summary>
        public int RaisedCount;

        /// <summary>
        /// Number of events dispatched.
        /// </summary>
        public int DispatchedCount;

        /// <summary>
        /// Number of subscribers.
        /// </summary>
        public int SubscriberCount;

        /// <summary>
        /// Dispatch time in milliseconds.
        /// </summary>
        public float DispatchTimeMs;
    }
}
