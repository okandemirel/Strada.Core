using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Strada.Core.DI
{
    internal static class TypeRegistry
    {
        private static int _nextId;
        private static readonly Dictionary<Type, int> _typeCache = new(256);
        private static readonly object _cacheLock = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetId<T>() => TypeId<T>.Id;

        public static int GetId(Type type)
        {
            if (_typeCache.TryGetValue(type, out int id))
                return id;

            lock (_cacheLock)
            {
                if (_typeCache.TryGetValue(type, out id))
                    return id;

                id = (int)typeof(TypeId<>)
                    .MakeGenericType(type)
                    .GetField("Id")
                    .GetValue(null);

                _typeCache[type] = id;
                return id;
            }
        }

        internal static int AllocateId() => Interlocked.Increment(ref _nextId);

        private static class TypeId<T>
        {
            public static readonly int Id;

            static TypeId()
            {
                Id = AllocateId();
                lock (_cacheLock)
                {
                    _typeCache[typeof(T)] = Id;
                }
            }
        }
    }
}
