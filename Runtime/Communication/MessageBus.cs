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

    public interface IMessageBus : IDisposable
    {
        void Send<TCommand>(ref TCommand command) where TCommand : struct;
        void Send<TCommand>(TCommand command) where TCommand : struct;
        TResult Query<TQuery, TResult>(ref TQuery query) where TQuery : struct, IQuery<TResult>;
        TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
        void Publish<TEvent>(ref TEvent message) where TEvent : struct;
        void Publish<TEvent>(TEvent message) where TEvent : struct;
        void Execute(ICommand command);

        [Obsolete("Use ExecuteAsync(IAsyncAwaitCommand, CancellationToken) instead for better async support")]
        void ExecuteAsync(IAsyncCommand command, Action onComplete = null);
        void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
        void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : struct;
        void RegisterQueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler) where TQuery : struct, IQuery<TResult>;
        void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler) where TQuery : struct, IQuery<TResult>;
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        int GetSubscriberCount<TEvent>() where TEvent : struct;
        void Clear();

        ValueTask SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : struct;
        void RegisterAsyncCommandHandler<TCommand>(IAsyncAwaitCommandHandler<TCommand> handler) where TCommand : struct;
        void RegisterAsyncCommandHandler<TCommand>(Func<TCommand, CancellationToken, ValueTask> handler) where TCommand : struct;
        ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default) where TQuery : struct, IAsyncQuery<TResult>;
        void RegisterAsyncQueryHandler<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult> handler) where TQuery : struct, IAsyncQuery<TResult>;
        void RegisterAsyncQueryHandler<TQuery, TResult>(Func<TQuery, CancellationToken, ValueTask<TResult>> handler) where TQuery : struct, IAsyncQuery<TResult>;
        ValueTask ExecuteAsync(IAsyncAwaitCommand command, CancellationToken cancellationToken = default);
    }

    public sealed class MessageBus : IMessageBus
    {
        // Separate type ID counters to avoid wasting array space and prevent theoretical collisions
        private static int _nextCommandTypeId;
        private static int _nextQueryTypeId;
        private static int _nextEventTypeId;
        private static int _nextAsyncCommandTypeId;
        private static int _nextAsyncQueryTypeId;

        private object[] _commandHandlers = new object[64];
        private object[] _queryHandlers = new object[64];
        private object[] _eventChannels = new object[64];
        private object[] _asyncCommandHandlers = new object[64];
        private object[] _asyncQueryHandlers = new object[64];
        private int _maxCommandId;
        private int _maxQueryId;
        private int _maxEventId;
        private int _maxAsyncCommandId;
        private int _maxAsyncQueryId;
        private bool _disposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(ref TCommand command) where TCommand : struct
        {
            var id = CommandTypeId<TCommand>.Id;
            if (id <= _maxCommandId && _commandHandlers[id] != null)
            {
                ((Action<TCommand>)_commandHandlers[id])(command);
                return;
            }
            ThrowHandlerNotFoundException<TCommand>("command");
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

        public void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct
        {
            var id = CommandTypeId<TCommand>.Id;
            EnsureCapacity(ref _commandHandlers, id);
            _commandHandlers[id] = handler;
            if (id > _maxCommandId) _maxCommandId = id;
        }

        public void RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : struct
        {
            RegisterCommandHandler<TCommand>(handler.Handle);
        }

        public void Execute(ICommand command)
        {
            command.Execute();
            if (command is IPooledCommand pooled)
                pooled.ReturnToPool();
        }

        public void ExecuteAsync(IAsyncCommand command, Action onComplete = null)
        {
            command.Execute(() =>
            {
                (command as IPooledCommand)?.ReturnToPool();
                onComplete?.Invoke();
            });
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
            Array.Clear(_commandHandlers, 0, _commandHandlers.Length);
            Array.Clear(_queryHandlers, 0, _queryHandlers.Length);
            Array.Clear(_eventChannels, 0, _eventChannels.Length);
            Array.Clear(_asyncCommandHandlers, 0, _asyncCommandHandlers.Length);
            Array.Clear(_asyncQueryHandlers, 0, _asyncQueryHandlers.Length);
            _maxCommandId = 0;
            _maxQueryId = 0;
            _maxEventId = 0;
            _maxAsyncCommandId = 0;
            _maxAsyncQueryId = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }

        public async ValueTask SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : struct
        {
            var id = AsyncCommandTypeId<TCommand>.Id;
            if (id <= _maxAsyncCommandId && _asyncCommandHandlers[id] != null)
            {
                await ((Func<TCommand, CancellationToken, ValueTask>)_asyncCommandHandlers[id])(command, cancellationToken);
                return;
            }
            ThrowHandlerNotFoundException<TCommand>("async command");
        }

        public void RegisterAsyncCommandHandler<TCommand>(IAsyncAwaitCommandHandler<TCommand> handler) where TCommand : struct
        {
            RegisterAsyncCommandHandler<TCommand>((cmd, ct) => handler.HandleAsync(cmd, ct));
        }

        public void RegisterAsyncCommandHandler<TCommand>(Func<TCommand, CancellationToken, ValueTask> handler) where TCommand : struct
        {
            var id = AsyncCommandTypeId<TCommand>.Id;
            EnsureCapacity(ref _asyncCommandHandlers, id);
            _asyncCommandHandlers[id] = handler;
            if (id > _maxAsyncCommandId) _maxAsyncCommandId = id;
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

        private static class AsyncCommandTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextAsyncCommandTypeId);
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

        private static class CommandTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextCommandTypeId);
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
