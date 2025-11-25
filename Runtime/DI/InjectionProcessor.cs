using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Strada.Core.DI.Attributes;

namespace Strada.Core.DI
{
    public static class InjectionProcessor
    {
        private static readonly Dictionary<Type, TypeInjectionInfo> _cache = new(64);
        private static readonly object _lock = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Inject(object target, IContainer container)
        {
            var type = target.GetType();
            var info = GetOrCreateInfo(type);

            InjectMethods(target, info.Methods, container);
            InjectProperties(target, info.Properties, container);
            InjectFields(target, info.Fields, container);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InjectInto<T>(T target, IContainer container) where T : class
        {
            var info = GetOrCreateInfo(typeof(T));

            InjectMethods(target, info.Methods, container);
            InjectProperties(target, info.Properties, container);
            InjectFields(target, info.Fields, container);
        }

        private static TypeInjectionInfo GetOrCreateInfo(Type type)
        {
            if (_cache.TryGetValue(type, out var info))
                return info;

            lock (_lock)
            {
                if (_cache.TryGetValue(type, out info))
                    return info;

                info = BuildInjectionInfo(type);
                _cache[type] = info;
                return info;
            }
        }

        private static TypeInjectionInfo BuildInjectionInfo(Type type)
        {
            var methods = new List<MethodInjectionInfo>(4);
            var properties = new List<PropertyInfo>(4);
            var fields = new List<FieldInfo>(4);

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var method in type.GetMethods(flags))
            {
                if (method.GetCustomAttribute<InjectAttribute>() == null)
                    continue;

                var parameters = method.GetParameters();
                var paramTypes = new Type[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                    paramTypes[i] = parameters[i].ParameterType;

                methods.Add(new MethodInjectionInfo(method, paramTypes));
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetCustomAttribute<InjectAttribute>() == null)
                    continue;

                if (property.CanWrite)
                    properties.Add(property);
            }

            foreach (var field in type.GetFields(flags))
            {
                if (field.GetCustomAttribute<InjectAttribute>() == null)
                    continue;

                fields.Add(field);
            }

            return new TypeInjectionInfo(methods.ToArray(), properties.ToArray(), fields.ToArray());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InjectMethods(object target, MethodInjectionInfo[] methods, IContainer container)
        {
            for (int i = 0; i < methods.Length; i++)
            {
                ref var method = ref methods[i];
                var args = ResolveParameters(method.ParameterTypes, container);
                method.Method.Invoke(target, args);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InjectProperties(object target, PropertyInfo[] properties, IContainer container)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                var value = container.Resolve(prop.PropertyType);
                prop.SetValue(target, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InjectFields(object target, FieldInfo[] fields, IContainer container)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var value = container.Resolve(field.FieldType);
                field.SetValue(target, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object[] ResolveParameters(Type[] types, IContainer container)
        {
            if (types.Length == 0)
                return Array.Empty<object>();

            var args = new object[types.Length];
            for (int i = 0; i < types.Length; i++)
                args[i] = container.Resolve(types[i]);

            return args;
        }

        public static void ClearCache()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        private readonly struct TypeInjectionInfo
        {
            public readonly MethodInjectionInfo[] Methods;
            public readonly PropertyInfo[] Properties;
            public readonly FieldInfo[] Fields;

            public TypeInjectionInfo(MethodInjectionInfo[] methods, PropertyInfo[] properties, FieldInfo[] fields)
            {
                Methods = methods;
                Properties = properties;
                Fields = fields;
            }
        }

        private struct MethodInjectionInfo
        {
            public readonly MethodInfo Method;
            public readonly Type[] ParameterTypes;

            public MethodInjectionInfo(MethodInfo method, Type[] parameterTypes)
            {
                Method = method;
                ParameterTypes = parameterTypes;
            }
        }
    }
}
