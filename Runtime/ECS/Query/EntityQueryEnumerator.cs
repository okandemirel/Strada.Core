using System;
using System.Collections;
using System.Collections.Generic;

namespace Strada.Core.ECS.Query
{
    public ref struct EntityQueryEnumerator<T1> where T1 : unmanaged, IStradaComponent
    {
        private readonly int[] _entities;
        private readonly T1[] _components1;
        private int _index;

        internal EntityQueryEnumerator(int[] entities, T1[] components1)
        {
            _entities = entities;
            _components1 = components1;
            _index = -1;
        }

        public bool MoveNext()
        {
            _index++;
            return _index < _entities.Length;
        }

        public int Entity => _entities[_index];

        public ref T1 C1 => ref _components1[_index];
    }

    public ref struct EntityQueryEnumerator<T1, T2>
        where T1 : unmanaged, IStradaComponent
        where T2 : unmanaged, IStradaComponent
    {
        private readonly int[] _entities;
        private readonly T1[] _components1;
        private readonly T2[] _components2;
        private int _index;

        internal EntityQueryEnumerator(int[] entities, T1[] components1, T2[] components2)
        {
            _entities = entities;
            _components1 = components1;
            _components2 = components2;
            _index = -1;
        }

        public bool MoveNext()
        {
            _index++;
            return _index < _entities.Length;
        }

        public int Entity => _entities[_index];

        public ref T1 C1 => ref _components1[_index];

        public ref T2 C2 => ref _components2[_index];
    }

    public ref struct EntityQueryEnumerator<T1, T2, T3>
        where T1 : unmanaged, IStradaComponent
        where T2 : unmanaged, IStradaComponent
        where T3 : unmanaged, IStradaComponent
    {
        private readonly int[] _entities;
        private readonly T1[] _components1;
        private readonly T2[] _components2;
        private readonly T3[] _components3;
        private int _index;

        internal EntityQueryEnumerator(int[] entities, T1[] components1, T2[] components2, T3[] components3)
        {
            _entities = entities;
            _components1 = components1;
            _components2 = components2;
            _components3 = components3;
            _index = -1;
        }

        public bool MoveNext()
        {
            _index++;
            return _index < _entities.Length;
        }

        public int Entity => _entities[_index];

        public ref T1 C1 => ref _components1[_index];

        public ref T2 C2 => ref _components2[_index];

        public ref T3 C3 => ref _components3[_index];
    }
}
