using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Strada.Core.DI
{
    public sealed class FastContainer : IContainer
    {
        private readonly Func<object>[] _factories;
        private readonly object[] _singletons;
        private readonly Lifetime[] _lifetimes;
        private readonly int[] _typeIdToIndex;
        private readonly int _maxTypeId;
        private readonly Type[] _registeredTypes;
        private int _registeredCount;
        private bool _disposed;

        internal FastContainer(Dictionary<Type, Registration> registrations)
        {
            var count = registrations.Count;
            _registeredTypes = new Type[count];
            _factories = new Func<object>[count];
            _singletons = new object[count];
            _lifetimes = new Lifetime[count];

            var typeIdMap = BuildTypeIdMap(registrations, out _maxTypeId);
            _typeIdToIndex = BuildIndexArray(_maxTypeId, typeIdMap);
            BuildFactories(registrations, typeIdMap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Resolve<T>() where T : class
        {
            if (_disposed) ThrowDisposed();
            var typeId = TypeId<T>.Id;
            if (typeId <= _maxTypeId)
            {
                var index = _typeIdToIndex[typeId];
                if (index >= 0)
                {
                    var singleton = _singletons[index];
                    if (singleton != null) return (T)singleton;
                    return (T)ResolveByIndexInternal(index);
                }
            }
            ThrowNotRegistered<T>();
            return default;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNotRegistered<T>() =>
            throw new InvalidOperationException($"Type '{typeof(T).Name}' is not registered");

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowDisposed() =>
            throw new ObjectDisposedException(nameof(FastContainer));

        public object Resolve(Type type)
        {
            if (_disposed) ThrowDisposed();
            return ResolveByType(type);
        }

        public bool TryResolve<T>(out T instance) where T : class
        {
            var typeId = TypeId<T>.Id;
            if (typeId <= _maxTypeId && _typeIdToIndex[typeId] >= 0)
            {
                instance = Resolve<T>();
                return true;
            }
            instance = null;
            return false;
        }

        public IContainerScope CreateScope()
        {
            if (_disposed) ThrowDisposed();
            return new FastContainerScope(this, _factories, _lifetimes, _typeIdToIndex, _maxTypeId, _singletons);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRegistered<T>() where T : class
        {
            var typeId = TypeId<T>.Id;
            return typeId <= _maxTypeId && _typeIdToIndex[typeId] >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRegistered(Type type)
        {
            var typeId = TypeRegistry.GetId(type);
            return typeId <= _maxTypeId && _typeIdToIndex[typeId] >= 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _registeredCount; i++)
                ClearFactory(_registeredTypes[i]);

            for (int i = 0; i < _singletons.Length; i++)
            {
                var obj = Volatile.Read(ref _singletons[i]);
                if (obj is IDisposable d) d.Dispose();
                _singletons[i] = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal object ResolveByIndex(int index)
        {
            var singleton = _singletons[index];
            if (singleton != null) return singleton;
            return ResolveByIndexInternal(index);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private object ResolveByIndexInternal(int index)
        {
            if (_lifetimes[index] == Lifetime.Scoped)
                throw new InvalidOperationException("Cannot resolve scoped type from root container. Use CreateScope() first.");

            var instance = _factories[index]();
            if (_lifetimes[index] != Lifetime.Singleton) return instance;

            var prev = Interlocked.CompareExchange(ref _singletons[index], instance, null);
            if (prev != null)
            {
                if (instance is IDisposable d) d.Dispose();
                return prev;
            }
            return instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object ResolveByType(Type type)
        {
            var typeId = TypeRegistry.GetId(type);
            if (typeId <= _maxTypeId)
            {
                var index = _typeIdToIndex[typeId];
                if (index >= 0) return ResolveByIndex(index);
            }
            throw new InvalidOperationException($"Type '{type.Name}' is not registered");
        }

        private Dictionary<int, int> BuildTypeIdMap(Dictionary<Type, Registration> registrations, out int maxId)
        {
            var map = new Dictionary<int, int>(registrations.Count);
            maxId = 0;
            int index = 0;

            foreach (var kvp in registrations)
            {
                int typeId = TypeRegistry.GetId(kvp.Key);
                map[typeId] = index;
                _registeredTypes[index] = kvp.Key;
                index++;
                if (typeId > maxId) maxId = typeId;
            }
            _registeredCount = index;
            return map;
        }

        private static int[] BuildIndexArray(int maxId, Dictionary<int, int> typeIdMap)
        {
            var arr = new int[maxId + 1];
            for (int i = 0; i <= maxId; i++) arr[i] = -1;
            foreach (var kvp in typeIdMap) arr[kvp.Key] = kvp.Value;
            return arr;
        }

        private void BuildFactories(Dictionary<Type, Registration> registrations, Dictionary<int, int> typeIdMap)
        {
            foreach (var kvp in registrations)
            {
                var reg = kvp.Value;
                int index = typeIdMap[TypeRegistry.GetId(kvp.Key)];
                _lifetimes[index] = reg.Lifetime;

                if (reg.Instance != null)
                {
                    _singletons[index] = reg.Instance;
                    _factories[index] = () => reg.Instance;
                }
                else if (reg.Factory != null)
                    _factories[index] = () => reg.Factory(this);
                else
                {
                    var directFactory = TryGetDirectFactory(kvp.Key);
                    _factories[index] = directFactory ?? CompileFactory(reg.ImplementationType, registrations, typeIdMap);
                }
            }
        }

        private Func<object> TryGetDirectFactory(Type serviceType)
        {
            var method = typeof(FastContainer).GetMethod(nameof(CreateDirectFactoryWrapper), BindingFlags.NonPublic | BindingFlags.Static);
            var genericMethod = method.MakeGenericMethod(serviceType);
            return (Func<object>)genericMethod.Invoke(null, new object[] { this });
        }

        private static Func<object> CreateDirectFactoryWrapper<T>(FastContainer container) where T : class
        {
            var factory = DirectFactory<T>.Delegate;
            if (factory == null) return null;
            return () => factory(container);
        }

        private Func<object> CompileFactory(Type implType, Dictionary<Type, Registration> regs, Dictionary<int, int> typeIdMap)
        {
            var ctor = GetBestConstructor(implType);
            var parameters = ctor.GetParameters();

            if (parameters.Length == 0)
                return Expression.Lambda<Func<object>>(Expression.New(ctor)).Compile();

            var args = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var pType = parameters[i].ParameterType;
                if (!regs.TryGetValue(pType, out var depReg))
                    throw new InvalidOperationException($"Dependency '{pType.Name}' not registered for '{implType.Name}'");
                args[i] = BuildDependencyExpr(pType, depReg, regs, typeIdMap);
            }
            return Expression.Lambda<Func<object>>(Expression.New(ctor, args)).Compile();
        }

        private static ConstructorInfo GetBestConstructor(Type type)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length == 0)
                throw new InvalidOperationException($"No public constructor for {type.Name}");

            if (ctors.Length == 1) return ctors[0];

            ConstructorInfo best = ctors[0];
            int bestCount = best.GetParameters().Length;

            for (int i = 1; i < ctors.Length; i++)
            {
                int count = ctors[i].GetParameters().Length;
                if (count > bestCount)
                {
                    best = ctors[i];
                    bestCount = count;
                }
            }
            return best;
        }

        private Expression BuildDependencyExpr(Type serviceType, Registration reg, Dictionary<Type, Registration> regs, Dictionary<int, int> typeIdMap)
        {
            if (reg.Instance != null)
                return Expression.Constant(reg.Instance, serviceType);

            if (reg.Lifetime == Lifetime.Singleton || reg.Factory != null)
            {
                int index = typeIdMap[TypeRegistry.GetId(serviceType)];
                return Expression.Convert(
                    Expression.Call(Expression.Constant(this), typeof(FastContainer).GetMethod(nameof(ResolveByIndex), BindingFlags.Instance | BindingFlags.NonPublic), Expression.Constant(index)),
                    serviceType);
            }

            var ctor = GetBestConstructor(reg.ImplementationType);
            var parameters = ctor.GetParameters();

            if (parameters.Length == 0)
                return Expression.New(ctor);

            var depArgs = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var pType = parameters[i].ParameterType;
                depArgs[i] = BuildDependencyExpr(pType, regs[pType], regs, typeIdMap);
            }
            return Expression.New(ctor, depArgs);
        }

        private static void ClearFactory(Type type) =>
            typeof(DirectFactory<>).MakeGenericType(type).GetField(nameof(DirectFactory<object>.Delegate)).SetValue(null, null);

        private static class TypeId<T>
        {
            public static readonly int Id = TypeRegistry.GetId<T>();
        }
    }
}
