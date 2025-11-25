using System.Runtime.CompilerServices;
using Strada.Core.ECS.Storage;

namespace Strada.Core.ECS.Query
{
    public readonly struct QueryBuilder
    {
        private readonly EntityManager _manager;

        internal QueryBuilder(EntityManager manager) => _manager = manager;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityQuery<T1> Select<T1>() where T1 : unmanaged, IComponent
        {
            var storage = _manager.Store.GetOrCreateStorage<T1>();
            return new EntityQuery<T1>(_manager, storage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityQuery<T1, T2> Select<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            var s1 = _manager.Store.GetOrCreateStorage<T1>();
            var s2 = _manager.Store.GetOrCreateStorage<T2>();
            return new EntityQuery<T1, T2>(_manager, s1, s2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityQuery<T1, T2, T3> Select<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            var s1 = _manager.Store.GetOrCreateStorage<T1>();
            var s2 = _manager.Store.GetOrCreateStorage<T2>();
            var s3 = _manager.Store.GetOrCreateStorage<T3>();
            return new EntityQuery<T1, T2, T3>(_manager, s1, s2, s3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FilteredQueryBuilder<T1> Filter<T1>() where T1 : unmanaged, IComponent
        {
            var storage = _manager.Store.GetOrCreateStorage<T1>();
            return new FilteredQueryBuilder<T1>(_manager, storage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FilteredQueryBuilder<T1, T2> Filter<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            var s1 = _manager.Store.GetOrCreateStorage<T1>();
            var s2 = _manager.Store.GetOrCreateStorage<T2>();
            return new FilteredQueryBuilder<T1, T2>(_manager, s1, s2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FilteredQueryBuilder<T1, T2, T3> Filter<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            var s1 = _manager.Store.GetOrCreateStorage<T1>();
            var s2 = _manager.Store.GetOrCreateStorage<T2>();
            var s3 = _manager.Store.GetOrCreateStorage<T3>();
            return new FilteredQueryBuilder<T1, T2, T3>(_manager, s1, s2, s3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityQuery<T1> With<T1>() where T1 : unmanaged, IComponent => Select<T1>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityQuery<T1, T2> With<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            => Select<T1, T2>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityQuery<T1, T2, T3> With<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            => Select<T1, T2, T3>();
    }

    public static class EntityManagerQueryExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QueryBuilder Query(this EntityManager manager) => new QueryBuilder(manager);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ForEach<T1>(this EntityManager em, QueryDelegate<T1> action)
            where T1 : unmanaged, IComponent
            => em.Query().Select<T1>().ForEach(action);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ForEach<T1, T2>(this EntityManager em, QueryDelegate<T1, T2> action)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            => em.Query().Select<T1, T2>().ForEach(action);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ForEach<T1, T2, T3>(this EntityManager em, QueryDelegate<T1, T2, T3> action)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            => em.Query().Select<T1, T2, T3>().ForEach(action);
    }
}
