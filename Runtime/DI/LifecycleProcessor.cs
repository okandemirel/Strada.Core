using System;
using System.Collections.Generic;
using System.Reflection;
using Strada.Core.DI.Attributes;

namespace Strada.Core.DI
{
    public static class LifecycleProcessor
    {
        private static readonly Dictionary<Type, MethodInfo[]> PostConstructCache = new();
        private static readonly Dictionary<Type, MethodInfo[]> DeConstructCache = new();
        private const BindingFlags MethodFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void InvokePostConstruct(object target)
        {
            if (target == null) return;

            var type = target.GetType();
            if (!PostConstructCache.TryGetValue(type, out var methods))
            {
                methods = FindMethodsWithAttribute<PostConstructAttribute>(type);
                PostConstructCache[type] = methods;
            }

            foreach (var method in methods)
                method.Invoke(target, null);
        }

        public static void InvokeDeConstruct(object target)
        {
            if (target == null) return;

            var type = target.GetType();
            if (!DeConstructCache.TryGetValue(type, out var methods))
            {
                methods = FindMethodsWithAttribute<DeConstructAttribute>(type);
                DeConstructCache[type] = methods;
            }

            foreach (var method in methods)
                method.Invoke(target, null);
        }

        private static MethodInfo[] FindMethodsWithAttribute<T>(Type type) where T : Attribute
        {
            var result = new List<MethodInfo>();
            var methods = type.GetMethods(MethodFlags);

            foreach (var method in methods)
            {
                if (method.GetCustomAttribute<T>() != null && method.GetParameters().Length == 0)
                    result.Add(method);
            }

            return result.ToArray();
        }

        public static void ClearCache()
        {
            PostConstructCache.Clear();
            DeConstructCache.Clear();
        }
    }
}
