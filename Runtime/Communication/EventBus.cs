using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Strada.Core.Commands;
using Strada.Core.DI;

namespace Strada.Core.Communication
{
    public interface IQuery<TResult> { }

    public interface IQueryHandler<TQuery, TResult> where TQuery : struct, IQuery<TResult>
    {
        TResult Handle(ref TQuery query);
    }

    /// <summary>
    /// Async query marker interface for queries that return results asynchronously.
    /// </summary>
    public interface IAsyncQuery<TResult> { }

    /// <summary>
    /// Async query handler using ValueTask for optimal performance.
    /// </summary>
    public interface IAsyncQueryHandler<TQuery, TResult> where TQuery : struct, IAsyncQuery<TResult>
    {
        ValueTask<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
    }

    public interface IEventBus : IDisposable
    {
        void Send<TSignal>(ref TSignal signal) where TSignal : struct;
        void Send<TSignal>(TSignal signal) where TSignal : struct;
        TResult Query<TQuery, TResult>(ref TQuery query) where TQuery : struct, IQuery<TResult>;
        TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
        void Publish<TEvent>(ref TEvent message) where TEvent : struct;
        void Publish<TEvent>(TEvent message) where TEvent : struct;
        void RegisterSignalHandler<TSignal>(Action<TSignal> handler) where TSignal : struct;
        void RegisterSignalHandler<TSignal>(ISignalHandler<TSignal> handler) where TSignal : struct;
        void RegisterQueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler) where TQuery : struct, IQuery<TResult>;
        void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler) where TQuery : struct, IQuery<TResult>;
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        int GetSubscriberCount<TEvent>() where TEvent : struct;
        void Clear();

        ValueTask SendAsync<TSignal>(TSignal signal, CancellationToken cancellationToken = default) where TSignal : struct;
        void RegisterAsyncSignalHandler<TSignal>(IAsyncSignalHandler<TSignal> handler) where TSignal : struct;
        void RegisterAsyncSignalHandler<TSignal>(Func<TSignal, CancellationToken, ValueTask> handler) where TSignal : struct;
        ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default) where TQuery : struct, IAsyncQuery<TResult>;
        void RegisterAsyncQueryHandler<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult> handler) where TQuery : struct, IAsyncQuery<TResult>;
        void RegisterAsyncQueryHandler<TQuery, TResult>(Func<TQuery, CancellationToken, ValueTask<TResult>> handler) where TQuery : struct, IAsyncQuery<TResult>;
        ValueTask ExecuteAsync(IAsyncAwaitCommand command, CancellationToken cancellationToken = default);
    }

    public sealed class EventBus : IEventBus
    {
        // Separate type ID counters to avoid wasting array space and prevent theoretical collisions
        private static int _nextSignalTypeId;
        private static int _nextQueryTypeId;
        private static int _nextEventTypeId;
        private static int _nextAsyncSignalTypeId;
        private static int _nextAsyncQueryTypeId;

        private object[] _signalHandlers = new object[64];
        private object[] _queryHandlers = new object[64];
        private object[] _eventChannels = new object[64];
        private object[] _asyncSignalHandlers = new object[64];
        private object[] _asyncQueryHandlers = new object[64];
        private int _maxSignalId;
        private int _maxQueryId;
        private int _maxEventId;
        private int _maxAsyncSignalId;
        private int _maxAsyncQueryId;
        private bool _disposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TSignal>(ref TSignal signal) where TSignal : struct
        {
            var id = SignalTypeId<TSignal>.Id;
            if (id <= _maxSignalId && _signalHandlers[id] != null)
            {
                ((Action<TSignal>)_signalHandlers[id])(signal);
                return;
            }
            ThrowHandlerNotFoundException<TSignal>("signal");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TSignal>(TSignal signal) where TSignal : struct
        {
            Send(ref signal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Query<TQuery, TResult>(ref TQuery query) where TQuery : struct, IQuery<TResult>
        {
            var id = QueryTypeId<TQuery>.Id;
            if (id <= _maxQueryId && _queryHandlers[id] != null)
                return ((IQueryHandler<TQuery, TResult>)_queryHandlers[id]).Handle(ref query);

            ThrowHandlerNotFoundException<TQuery>("query");
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>
        {
            return Query<TQuery, TResult>(ref query);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(ref TEvent message) where TEvent : struct
        {
            var id = EventTypeId<TEvent>.Id;
            if (id > _maxEventId) return;

            var channel = _eventChannels[id] as EventChannel<TEvent>;
            channel?.Publish(ref message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(TEvent message) where TEvent : struct
        {
            Publish(ref message);
        }

        public void RegisterSignalHandler<TSignal>(Action<TSignal> handler) where TSignal : struct
        {
            var id = SignalTypeId<TSignal>.Id;
            EnsureCapacity(ref _signalHandlers, id);
            _signalHandlers[id] = handler;
            if (id > _maxSignalId) _maxSignalId = id;
        }

        public void RegisterSignalHandler<TSignal>(ISignalHandler<TSignal> handler) where TSignal : struct
        {
            RegisterSignalHandler<TSignal>(handler.Handle);
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

            (_eventChannels[id] as EventChannel<TEvent>)?.Unsubscribe(handler);
        }

        public int GetSubscriberCount<TEvent>() where TEvent : struct
        {
            var id = EventTypeId<TEvent>.Id;
            if (id > _maxEventId) return 0;

            var channel = _eventChannels[id] as EventChannel<TEvent>;
            return channel?.Count ?? 0;
        }

        public void Clear()
        {
            Array.Clear(_signalHandlers, 0, _signalHandlers.Length);
            Array.Clear(_queryHandlers, 0, _queryHandlers.Length);
            Array.Clear(_eventChannels, 0, _eventChannels.Length);
            Array.Clear(_asyncSignalHandlers, 0, _asyncSignalHandlers.Length);
            Array.Clear(_asyncQueryHandlers, 0, _asyncQueryHandlers.Length);
            _maxSignalId = 0;
            _maxQueryId = 0;
            _maxEventId = 0;
            _maxAsyncSignalId = 0;
            _maxAsyncQueryId = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }

        public async ValueTask SendAsync<TSignal>(TSignal signal, CancellationToken cancellationToken = default) where TSignal : struct
        {
            var id = AsyncSignalTypeId<TSignal>.Id;
            if (id <= _maxAsyncSignalId && _asyncSignalHandlers[id] != null)
            {
                await ((Func<TSignal, CancellationToken, ValueTask>)_asyncSignalHandlers[id])(signal, cancellationToken);
                return;
            }
            ThrowHandlerNotFoundException<TSignal>("async signal");
        }

        public void RegisterAsyncSignalHandler<TSignal>(IAsyncSignalHandler<TSignal> handler) where TSignal : struct
        {
            RegisterAsyncSignalHandler<TSignal>((signal, ct) => handler.HandleAsync(signal, ct));
        }

        public void RegisterAsyncSignalHandler<TSignal>(Func<TSignal, CancellationToken, ValueTask> handler) where TSignal : struct
        {
            var id = AsyncSignalTypeId<TSignal>.Id;
            EnsureCapacity(ref _asyncSignalHandlers, id);
            _asyncSignalHandlers[id] = handler;
            if (id > _maxAsyncSignalId) _maxAsyncSignalId = id;
        }

        public async ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
            where TQuery : struct, IAsyncQuery<TResult>
        {
            var id = AsyncQueryTypeId<TQuery>.Id;
            if (id <= _maxAsyncQueryId && _asyncQueryHandlers[id] != null)
            {
                return await ((Func<TQuery, CancellationToken, ValueTask<TResult>>)_asyncQueryHandlers[id])(query, cancellationToken);
            }
            ThrowHandlerNotFoundException<TQuery>("async query");
            return default;
        }

        public void RegisterAsyncQueryHandler<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IAsyncQuery<TResult>
        {
            RegisterAsyncQueryHandler<TQuery, TResult>((q, ct) => handler.HandleAsync(q, ct));
        }

        public void RegisterAsyncQueryHandler<TQuery, TResult>(Func<TQuery, CancellationToken, ValueTask<TResult>> handler)
            where TQuery : struct, IAsyncQuery<TResult>
        {
            var id = AsyncQueryTypeId<TQuery>.Id;
            EnsureCapacity(ref _asyncQueryHandlers, id);
            _asyncQueryHandlers[id] = handler;
            if (id > _maxAsyncQueryId) _maxAsyncQueryId = id;
        }

        public ValueTask ExecuteAsync(IAsyncAwaitCommand command, CancellationToken cancellationToken = default)
        {
            return command.ExecuteAsync(cancellationToken);
        }

        private static class AsyncSignalTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextAsyncSignalTypeId);
        }

        private static class AsyncQueryTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextAsyncQueryTypeId);
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
        private static void ThrowHandlerNotFoundException<T>(string type) =>
            throw new InvalidOperationException($"No {type} handler registered for '{typeof(T).Name}'");

        private static class SignalTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextSignalTypeId);
        }

        private static class QueryTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextQueryTypeId);
        }

        private static class EventTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextEventTypeId);
        }

        private sealed class EventChannel<T>
        {
            private Action<T>[] _handlers = new Action<T>[8];
            private int _count;

            public int Count => _count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Publish(ref T message)
            {
                var count = _count;
                var handlers = _handlers;
                for (int i = 0; i < count; i++)
                    handlers[i](message);
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

                    _count--;
                    if (i < _count)
                        _handlers[i] = _handlers[_count];
                    _handlers[_count] = null;
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
