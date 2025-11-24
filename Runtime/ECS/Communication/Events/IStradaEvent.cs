using System;

namespace Strada.Core.ECS.Communication
{
    public interface IStradaEvent
    {
    }

    public interface IEventPublisher
    {
        void Raise<T>(T eventData) where T : struct, IStradaEvent;
        int PendingCount { get; }
        void Clear();
    }

    public interface IEventSubscriber
    {
        void Subscribe<T>(Action<T> handler) where T : struct, IStradaEvent;
        void Unsubscribe<T>(Action<T> handler) where T : struct, IStradaEvent;
        void UnsubscribeAll<T>() where T : struct, IStradaEvent;
        bool HasSubscribers<T>() where T : struct, IStradaEvent;
        int GetSubscriberCount<T>() where T : struct, IStradaEvent;
    }

    public interface IEventDispatcher
    {
        void DispatchPendingEvents();
        void DispatchEvents<T>() where T : struct, IStradaEvent;
    }

    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class EventRaiserAttribute : Attribute
    {
        public Type[] EventTypes { get; }

        public EventRaiserAttribute(params Type[] eventTypes)
        {
            EventTypes = eventTypes ?? Array.Empty<Type>();

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

    public enum EventPriority
    {
        Low = 0,
        Normal = 50,
        High = 100
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class EventPriorityAttribute : Attribute
    {
        public EventPriority Priority { get; }

        public EventPriorityAttribute(EventPriority priority)
        {
            Priority = priority;
        }
    }

    public struct EventProcessingInfo
    {
        public Type EventType;
        public int RaisedCount;
        public int DispatchedCount;
        public int SubscriberCount;
        public float DispatchTimeMs;
    }
}
