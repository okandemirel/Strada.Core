using System;
using UnityEngine;

namespace Strada.Core.ECS.Communication
{
    public static class StradaEvents
    {
        private static StradaEventBus _globalBus;

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

        public static void Raise<T>(T eventData) where T : struct, IStradaEvent
        {
            Global.Raise(eventData);
        }

        public static void Subscribe<T>(Action<T> handler) where T : struct, IStradaEvent
        {
            Global.Subscribe(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IStradaEvent
        {
            Global.Unsubscribe(handler);
        }

        public static void UnsubscribeAll<T>() where T : struct, IStradaEvent
        {
            Global.UnsubscribeAll<T>();
        }

        public static bool HasSubscribers<T>() where T : struct, IStradaEvent
        {
            return Global.HasSubscribers<T>();
        }

        public static int GetSubscriberCount<T>() where T : struct, IStradaEvent
        {
            return Global.GetSubscriberCount<T>();
        }

        public static void DispatchPendingEvents()
        {
            Global.DispatchPendingEvents();
        }

        public static void DispatchEvents<T>() where T : struct, IStradaEvent
        {
            Global.DispatchEvents<T>();
        }

        public static void Clear()
        {
            Global.Clear();
        }
    }
}
