using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.DI;

namespace Strada.Core.Communication
{
    public interface ICommandData { }

    public interface ICommandHandler<T> where T : struct, ICommandData
    {
        void Execute(ref T command);
    }

    public sealed class CommandDispatcher : IDisposable
    {
        private readonly Dictionary<Type, object> _handlers = new(32);
        private readonly Queue<(Type type, object command)> _pendingCommands = new(64);
        private readonly IContainer _container;
        private bool _processing;
        private bool _disposed;

        public CommandDispatcher() { }

        public CommandDispatcher(IContainer container)
        {
            _container = container;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterHandler<T>(ICommandHandler<T> handler) where T : struct, ICommandData
        {
            _handlers[typeof(T)] = handler;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterHandler<T>(Action<T> handler) where T : struct, ICommandData
        {
            _handlers[typeof(T)] = new DelegateHandler<T>(handler);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<T>(T command) where T : struct, ICommandData
        {
            if (_processing)
            {
                _pendingCommands.Enqueue((typeof(T), command));
                return;
            }

            ExecuteCommand(ref command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<T>(ref T command) where T : struct, ICommandData
        {
            if (_processing)
            {
                _pendingCommands.Enqueue((typeof(T), command));
                return;
            }

            ExecuteCommand(ref command);
        }

        public void ProcessPendingCommands()
        {
            while (_pendingCommands.Count > 0)
            {
                var (type, command) = _pendingCommands.Dequeue();
                ProcessCommand(type, command);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExecuteCommand<T>(ref T command) where T : struct, ICommandData
        {
            _processing = true;

            if (_handlers.TryGetValue(typeof(T), out var handler))
            {
                ((ICommandHandler<T>)handler).Execute(ref command);
            }
            else if (_container != null && _container.TryResolve<ICommandHandler<T>>(out var resolved))
            {
                resolved.Execute(ref command);
            }

            _processing = false;
            ProcessPendingCommands();
        }

        private void ProcessCommand(Type type, object command)
        {
            if (!_handlers.TryGetValue(type, out var handler))
                return;

            var method = handler.GetType().GetMethod("Execute");
            method?.Invoke(handler, new[] { command });
        }

        public void Clear()
        {
            _handlers.Clear();
            _pendingCommands.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }

        private sealed class DelegateHandler<T> : ICommandHandler<T> where T : struct, ICommandData
        {
            private readonly Action<T> _handler;

            public DelegateHandler(Action<T> handler) => _handler = handler;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(ref T command) => _handler(command);
        }
    }
}
