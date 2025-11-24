using System;
using System.Runtime.CompilerServices;
using Strada.Core.ECS.Storage;

namespace Strada.Core.ECS.Query
{
    public readonly struct FastEntityQuery<T1> where T1 : unmanaged, IStradaComponent
    {
        private readonly int[] _entities;
        private readonly T1[] _components1;
        private readonly int _count;

        internal FastEntityQuery(int[] entities, T1[] components1, int count)
        {
            _entities = entities;
            _components1 = components1;
            _count = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastQueryEnumerator<T1> GetEnumerator()
        {
            return new FastQueryEnumerator<T1>(_entities, _components1, _count);
        }

        public int Count => _count;
    }

    public readonly struct FastEntityQuery<T1, T2>
        where T1 : unmanaged, IStradaComponent
        where T2 : unmanaged, IStradaComponent
    {
        private readonly int[] _entities;
        private readonly T1[] _components1;
        private readonly T2[] _components2;
        private readonly int _count;

        internal FastEntityQuery(int[] entities, T1[] components1, T2[] components2, int count)
        {
            _entities = entities;
            _components1 = components1;
            _components2 = components2;
            _count = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastQueryEnumerator<T1, T2> GetEnumerator()
        {
            return new FastQueryEnumerator<T1, T2>(_entities, _components1, _components2, _count);
        }

        public int Count => _count;
    }

    public ref struct FastQueryEnumerator<T1> where T1 : unmanaged, IStradaComponent
    {
        private readonly int[] _entities;
        private readonly T1[] _components1;
        private readonly int _count;
        private int _index;

        internal FastQueryEnumerator(int[] entities, T1[] components1, int count)
        {
            _entities = entities;
            _components1 = components1;
            _count = count;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _index++;
            return _index < _count;
        }

        public readonly ref T1 Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _components1[_index];
        }

        public readonly int Entity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _entities[_index];
        }
    }

    public ref struct FastQueryEnumerator<T1, T2>
        where T1 : unmanaged, IStradaComponent
        where T2 : unmanaged, IStradaComponent
    {
        private readonly int[] _entities;
        private readonly T1[] _components1;
        private readonly T2[] _components2;
        private readonly int _count;
        private int _index;

        internal FastQueryEnumerator(int[] entities, T1[] components1, T2[] components2, int count)
        {
            _entities = entities;
            _components1 = components1;
            _components2 = components2;
            _count = count;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _index++;
            return _index < _count;
        }

        public readonly int Entity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _entities[_index];
        }

        public readonly ref T1 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _components1[_index];
        }

        public readonly ref T2 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _components2[_index];
        }
    }
}
