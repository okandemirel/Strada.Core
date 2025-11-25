using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Strada.Core.DI;

namespace Strada.Core.Commands
{
    public interface ICommandBus
    {
        void Send<TCommand>(TCommand command) where TCommand : struct;
        void Send<TCommand>(ref TCommand command) where TCommand : struct;
        void Execute(ICommand command);
        void ExecuteAsync(IAsyncCommand command, Action onComplete = null);
    }

    public sealed class CommandBus : ICommandBus, IDisposable
    {
        private static int _nextTypeId;
        private object[] _handlers = new object[64];
        private object[] _asyncHandlers = new object[64];
        private int _maxId;
        private int _maxAsyncId;
        private readonly IContainer _container;
        private bool _disposed;

        public CommandBus() { }

        public CommandBus(IContainer container)
        {
            _container = container;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(TCommand command) where TCommand : struct
        {
            Send(ref command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(ref TCommand command) where TCommand : struct
        {
            var id = TypeId<TCommand>.Id;
            if (id <= _maxId && _handlers[id] != null)
            {
                ((ICommandHandler<TCommand>)_handlers[id]).Handle(command);
                return;
            }

            if (_container != null && _container.TryResolve<ICommandHandler<TCommand>>(out var resolved))
            {
                resolved.Handle(command);
                return;
            }

            ThrowNoHandler<TCommand>();
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
                if (command is IPooledCommand pooled)
                    pooled.ReturnToPool();
                onComplete?.Invoke();
            });
        }

        public void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : struct
        {
            var id = TypeId<TCommand>.Id;
            EnsureCapacity(ref _handlers, id);
            _handlers[id] = handler;
            if (id > _maxId) _maxId = id;
        }

        public void RegisterHandler<TCommand>(Action<TCommand> handler) where TCommand : struct
        {
            RegisterHandler(new DelegateHandler<TCommand>(handler));
        }

        public void RegisterAsyncHandler<TCommand>(IAsyncCommandHandler<TCommand> handler) where TCommand : struct
        {
            var id = AsyncTypeId<TCommand>.Id;
            EnsureCapacity(ref _asyncHandlers, id);
            _asyncHandlers[id] = handler;
            if (id > _maxAsyncId) _maxAsyncId = id;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Array.Clear(_handlers, 0, _handlers.Length);
            Array.Clear(_asyncHandlers, 0, _asyncHandlers.Length);
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
        private static void ThrowNoHandler<T>() =>
            throw new InvalidOperationException($"No handler registered for command '{typeof(T).Name}'");

        private static class TypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextTypeId);
        }

        private static class AsyncTypeId<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _nextTypeId);
        }

        private sealed class DelegateHandler<TCommand> : ICommandHandler<TCommand> where TCommand : struct
        {
            private readonly Action<TCommand> _handler;

            public DelegateHandler(Action<TCommand> handler) => _handler = handler;

            public void Handle(TCommand command) => _handler(command);
        }
    }
}
