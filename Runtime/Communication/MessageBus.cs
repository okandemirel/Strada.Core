using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Strada.Core.Communication
{
    public interface IMessageBus : IDisposable
    {
        void Send<TCommand>(ref TCommand command) where TCommand : struct;
        TResult Query<TQuery, TResult>(ref TQuery query) where TQuery : struct, IQuery<TResult>;
        void Publish<TEvent>(ref TEvent evt) where TEvent : struct;
        void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
        void RegisterQueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler) where TQuery : struct, IQuery<TResult>;
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
    }

    public sealed class MessageBus : IMessageBus
    {
        private static int _nextTypeId;
        private object[] _commandHandlers = new object[64];
        private object[] _queryHandlers = new object[64];
        private object[] _eventChannels = new object[64];
        private int _maxCommandId;
        private int _maxQueryId;
        private int _maxEventId;
        private bool _disposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(ref TCommand command) where TCommand : struct
        {
            var id = CommandTypeId<TCommand>.Id;
            if (id > _maxCommandId || _commandHandlers[id] == null)
                ThrowNoHandler<TCommand>("command");

            ((Action<TCommand>)_commandHandlers[id])(command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(TCommand command) where TCommand : struct
        {
            Send(ref command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Query<TQuery, TResult>(ref TQuery query) where TQuery : struct, IQuery<TResult>
        {
            var id = QueryTypeId<TQuery>.Id;
            if (id > _maxQueryId || _queryHandlers[id] == null)
                ThrowNoHandler<TQuery>("query");

            return ((IQueryHandler<TQuery, TResult>)_queryHandlers[id]).Handle(ref query);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>
        {
            return Query<TQuery, TResult>(ref query);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(ref TEvent evt) where TEvent : struct
        {
            var id = EventTypeId<TEvent>.Id;
            if (id > _maxEventId) return;

            var channel = _eventChannels[id] as EventChannel<TEvent>;
            if (channel == null) return;

            channel.Publish(ref evt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            Publish(ref evt);
        }

        public void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct
        {
            var id = CommandTypeId<TCommand>.Id;
            EnsureCapacity(ref _commandHandlers, id);
            _commandHandlers[id] = handler;
            if (id > _maxCommandId) _maxCommandId = id;
        }

        public void RegisterQueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            var id = QueryTypeId<TQuery>.Id;
            EnsureCapacity(ref _queryHandlers, id);
            _queryHandlers[id] = handler;
            if (id > _maxQueryId) _maxQueryId = id;
        }

        public void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            RegisterQueryHandler(new DelegateQueryHandler<TQuery, TResult>(handler));
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            var id = EventTypeId<TEvent>.Id;
            EnsureCapacity(ref _eventChannels, id);
            if (id > _maxEventId) _maxEventId = id;

            var channel = _eventChannels[id] as EventChannel<TEvent>;
            if (channel == null)
            {
                channel = new EventChannel<TEvent>();
                _eventChannels[id] = channel;
            }

            channel.Subscribe(handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            var id = EventTypeId<TEvent>.Id;
            if (id > _maxEventId) return;

            var channel = _eventChannels[id] as EventChannel<TEvent>;
            channel?.Unsubscribe(handler);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Array.Clear(_commandHandlers, 0, _commandHandlers.Length);
            Array.Clear(_queryHandlers, 0, _queryHandlers.Length);
            Array.Clear(_eventChannels, 0, _eventChannels.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureCapacity(ref object[] array, int id)
        {
            if (id < array.Length) return;
            var newSize = array.Length;
            while (newSize <= id) newSize *= 2;
            var newArray = new object[newSize];
            Array.Copy(array, newArray, array.Length);
            array = newArray;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNoHandler<T>(string type) =>
            throw new InvalidOperationException($"No {type} handler registered for '{typeof(T).Name}'");

        private static class CommandTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextTypeId);
        }

        private static class QueryTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextTypeId);
        }

        private static class EventTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextTypeId);
        }

        private sealed class EventChannel<T>
        {
            private Action<T>[] _handlers = new Action<T>[8];
            private int _count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Publish(ref T evt)
            {
                var count = _count;
                var handlers = _handlers;
                for (int i = 0; i < count; i++)
                    handlers[i](evt);
            }

            public void Subscribe(Action<T> handler)
            {
                if (_count >= _handlers.Length)
                {
                    var newHandlers = new Action<T>[_handlers.Length * 2];
                    Array.Copy(_handlers, newHandlers, _count);
                    _handlers = newHandlers;
                }
                _handlers[_count++] = handler;
            }

            public void Unsubscribe(Action<T> handler)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_handlers[i], handler)) continue;

                    for (int j = i; j < _count - 1; j++)
                        _handlers[j] = _handlers[j + 1];

                    _handlers[--_count] = null;
                    return;
                }
            }
        }

        private sealed class DelegateQueryHandler<TQuery, TResult> : IQueryHandler<TQuery, TResult>
            where TQuery : struct, IQuery<TResult>
        {
            private readonly Func<TQuery, TResult> _handler;

            public DelegateQueryHandler(Func<TQuery, TResult> handler) => _handler = handler;

            public TResult Handle(ref TQuery query) => _handler(query);
        }
    }
}
