using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Strada.Core.DI
{
    internal static class TypeRegistry
    {
        private const int MaxTypeCount = 8192;
        private static int _nextId;
        private static readonly ConcurrentDictionary<Type, int> _typeCache = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetId<T>() => TypeId<T>.Id;

        public static int GetId(Type type)
        {
            return _typeCache.GetOrAdd(type, static t =>
                (int)typeof(TypeId<>)
                    .MakeGenericType(t)
                    .GetField("Id")
                    .GetValue(null));
        }

        internal static int AllocateId()
        {
            int id = Interlocked.Increment(ref _nextId);
            if (id > MaxTypeCount)
                throw new InvalidOperationException("Maximum number of registered types (8192) exceeded");
            return id;
        }

        private static class TypeId<T>
        {
            public static readonly int Id;

            static TypeId()
            {
                Id = AllocateId();
                _typeCache[typeof(T)] = Id;
            }
        }
    }
}
