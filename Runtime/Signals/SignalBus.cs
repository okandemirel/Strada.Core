using System;
using System.Runtime.CompilerServices;

namespace Strada.Core.Signals
{
    public interface IStructSignal { }

    public sealed class SignalBus : IDisposable
    {
        private static class Cache<T> where T : struct, IStructSignal
        {
            public static Action<T>[] Handlers = Array.Empty<Action<T>>();
            public static int Count;
            public static readonly object Lock = new();
        }

        private bool _disposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fire<T>(T signal) where T : struct, IStructSignal
        {
            var handlers = Cache<T>.Handlers;
            var count = Cache<T>.Count;
            for (int i = 0; i < count; i++)
                handlers[i](signal);
        }

        public SignalBinding<T> Subscribe<T>(Action<T> handler) where T : struct, IStructSignal
        {
            lock (Cache<T>.Lock)
            {
                var handlers = Cache<T>.Handlers;
                var count = Cache<T>.Count;

                if (count >= handlers.Length)
                {
                    int newSize = handlers.Length == 0 ? 4 : handlers.Length * 2;
                    var newHandlers = new Action<T>[newSize];
                    Array.Copy(handlers, newHandlers, count);
                    Cache<T>.Handlers = newHandlers;
                    handlers = newHandlers;
                }

                handlers[count] = handler;
                Cache<T>.Count = count + 1;

                return new SignalBinding<T>(this, handler);
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, IStructSignal
        {
            lock (Cache<T>.Lock)
            {
                var handlers = Cache<T>.Handlers;
                var count = Cache<T>.Count;

                for (int i = 0; i < count; i++)
                {
                    if (handlers[i] == handler)
                    {
                        count--;
                        if (i < count)
                            handlers[i] = handlers[count];
                        handlers[count] = null;
                        Cache<T>.Count = count;
                        return;
                    }
                }
            }
        }

        public void Clear<T>() where T : struct, IStructSignal
        {
            lock (Cache<T>.Lock)
            {
                Array.Clear(Cache<T>.Handlers, 0, Cache<T>.Count);
                Cache<T>.Count = 0;
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
