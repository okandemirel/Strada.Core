using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Strada.Core.Commands
{
    public interface IPooledCommand : ICommand
    {
        void Reset();
        void ReturnToPool();
    }

    public sealed class CommandPool<T> where T : class, IPooledCommand, new()
    {
        private static CommandPool<T> _instance;
        public static CommandPool<T> Instance => _instance ??= new CommandPool<T>();

        private readonly Stack<T> _available = new(32);
        private readonly object _lock = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            lock (_lock)
            {
                return _available.Count > 0 ? _available.Pop() : new T();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T command)
        {
            command.Reset();
            lock (_lock)
            {
                _available.Push(command);
            }
        }

        public void Prewarm(int count)
        {
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                    _available.Push(new T());
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _available.Clear();
            }
        }
    }

    public abstract class PooledCommandBase<TSelf> : IPooledCommand where TSelf : PooledCommandBase<TSelf>, new()
    {
        public void Execute()
        {
            OnExecute();
        }

        public void ReturnToPool()
        {
            CommandPool<TSelf>.Instance.Return((TSelf)this);
        }

        public abstract void Reset();
        protected abstract void OnExecute();

        public static TSelf Rent() => CommandPool<TSelf>.Instance.Rent();
    }
}
