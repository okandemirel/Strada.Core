using System.Runtime.CompilerServices;
using Strada.Core.ECS.Storage;

namespace Strada.Core.ECS.Query
{
    public readonly struct EntityQuery<T1>
        where T1 : unmanaged, IComponent
    {
        private readonly ComponentStorage<T1> _storage1;

        internal EntityQuery(ComponentStorage<T1> storage1)
        {
            _storage1 = storage1;
        }

        public int Count => _storage1.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEach(QueryDelegate<T1> action)
        {
            ref var sparseSet = ref _storage1.GetSparseSet();
            int count = sparseSet.Count;

            unsafe
            {
                int* entities = sparseSet.GetDenseEntityPtr();
                T1* data = sparseSet.GetDataPtr();

                for (int i = 0; i < count; i++)
                {
                    action(entities[i], ref data[i]);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachReadOnly(QueryDelegateReadOnly<T1> action)
        {
            ref var sparseSet = ref _storage1.GetSparseSet();
            int count = sparseSet.Count;

            unsafe
            {
                int* entities = sparseSet.GetDenseEntityReadOnlyPtr();
                T1* data = sparseSet.GetDataReadOnlyPtr();

                for (int i = 0; i < count; i++)
                {
                    action(entities[i], in data[i]);
                }
            }
        }
    }

    public readonly struct EntityQuery<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        private readonly ComponentStorage<T1> _storage1;
        private readonly ComponentStorage<T2> _storage2;

        internal EntityQuery(ComponentStorage<T1> storage1, ComponentStorage<T2> storage2)
        {
            _storage1 = storage1;
            _storage2 = storage2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEach(QueryDelegate<T1, T2> action)
        {
            ref var set1 = ref _storage1.GetSparseSet();
            ref var set2 = ref _storage2.GetSparseSet();

            bool useSet1 = set1.Count <= set2.Count;

            unsafe
            {
                int* entities = useSet1 ? set1.GetDenseEntityPtr() : set2.GetDenseEntityPtr();
                int count = useSet1 ? set1.Count : set2.Count;

                for (int i = 0; i < count; i++)
                {
                    int entityIndex = entities[i];

                    int idx1 = set1.GetDenseIndex(entityIndex);
                    int idx2 = set2.GetDenseIndex(entityIndex);

                    if (idx1 < 0 || idx2 < 0)
                        continue;

                    T1* ptr1 = set1.GetDataPtr() + idx1;
                    T2* ptr2 = set2.GetDataPtr() + idx2;

                    action(entityIndex, ref *ptr1, ref *ptr2);
                }
            }
        }
    }

    public readonly struct EntityQuery<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        private readonly ComponentStorage<T1> _storage1;
        private readonly ComponentStorage<T2> _storage2;
        private readonly ComponentStorage<T3> _storage3;

        internal EntityQuery(
            ComponentStorage<T1> storage1,
            ComponentStorage<T2> storage2,
            ComponentStorage<T3> storage3)
        {
            _storage1 = storage1;
            _storage2 = storage2;
            _storage3 = storage3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEach(QueryDelegate<T1, T2, T3> action)
        {
            ref var set1 = ref _storage1.GetSparseSet();
            ref var set2 = ref _storage2.GetSparseSet();
            ref var set3 = ref _storage3.GetSparseSet();

            int count1 = set1.Count;
            int count2 = set2.Count;
            int count3 = set3.Count;

            unsafe
            {
                int minCount;
                int* entities;

                if (count1 <= count2 && count1 <= count3)
                {
                    entities = set1.GetDenseEntityPtr();
                    minCount = count1;
                }
                else if (count2 <= count3)
                {
                    entities = set2.GetDenseEntityPtr();
                    minCount = count2;
                }
                else
                {
                    entities = set3.GetDenseEntityPtr();
                    minCount = count3;
                }

                for (int i = 0; i < minCount; i++)
                {
                    int entityIndex = entities[i];

                    int idx1 = set1.GetDenseIndex(entityIndex);
                    int idx2 = set2.GetDenseIndex(entityIndex);
                    int idx3 = set3.GetDenseIndex(entityIndex);

                    if (idx1 < 0 || idx2 < 0 || idx3 < 0)
                        continue;

                    T1* ptr1 = set1.GetDataPtr() + idx1;
                    T2* ptr2 = set2.GetDataPtr() + idx2;
                    T3* ptr3 = set3.GetDataPtr() + idx3;

                    action(entityIndex, ref *ptr1, ref *ptr2, ref *ptr3);
                }
            }
        }
    }

    public delegate void QueryDelegate<T1>(int entityIndex, ref T1 c1) where T1 : unmanaged;
    public delegate void QueryDelegateReadOnly<T1>(int entityIndex, in T1 c1) where T1 : unmanaged;
    public delegate void QueryDelegate<T1, T2>(int entityIndex, ref T1 c1, ref T2 c2)
        where T1 : unmanaged where T2 : unmanaged;
    public delegate void QueryDelegate<T1, T2, T3>(int entityIndex, ref T1 c1, ref T2 c2, ref T3 c3)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged;
}
