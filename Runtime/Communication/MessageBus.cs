using System;
using System.Collections.Generic;
using Strada.Core.ECS;

namespace Strada.Core.Communication
{
    public sealed class MessageBus : IDisposable
    {
        private readonly Queue<IStradaCommand> _commandQueue;
        private readonly Dictionary<Type, List<Delegate>> _eventHandlers;
        private readonly List<IStradaEvent> _eventQueue;
        private bool _disposed;

        public MessageBus()
        {
            _commandQueue = new Queue<IStradaCommand>(256);
            _eventHandlers = new Dictionary<Type, List<Delegate>>(32);
            _eventQueue = new List<IStradaEvent>(256);
        }

        public void SendCommand<T>(T command) where T : IStradaCommand
        {
            _commandQueue.Enqueue(command);
        }

        public void Subscribe<T>(Action<T> handler) where T : IStradaEvent
        {
            var eventType = typeof(T);
            if (!_eventHandlers.ContainsKey(eventType))
            {
                _eventHandlers[eventType] = new List<Delegate>();
            }
            _eventHandlers[eventType].Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IStradaEvent
        {
            var eventType = typeof(T);
            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
            }
        }

        public void PublishEvent<T>(T @event) where T : IStradaEvent
        {
            _eventQueue.Add(@event);
        }

        public void ProcessCommands(EntityManager entityManager)
        {
            while (_commandQueue.Count > 0)
            {
                var command = _commandQueue.Dequeue();
                command.Execute(entityManager);
            }
        }

        public void DispatchEvents()
        {
            for (int i = 0; i < _eventQueue.Count; i++)
            {
                var @event = _eventQueue[i];
                var eventType = @event.GetType();

                if (_eventHandlers.TryGetValue(eventType, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        ((Delegate)handler).DynamicInvoke(@event);
                    }
                }
            }
            _eventQueue.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _commandQueue.Clear();
            _eventHandlers.Clear();
            _eventQueue.Clear();
            _disposed = true;
        }
    }
}
