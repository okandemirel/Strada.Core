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
        private static readonly object _lock = new();
        private const BindingFlags MethodFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void InvokePostConstruct(object target)
        {
            if (target == null) return;

            var type = target.GetType();
            var methods = GetOrCacheMethods(type, PostConstructCache, typeof(PostConstructAttribute));

            foreach (var method in methods)
            {
                try
                {
                    method.Invoke(target, null);
                }
                catch (TargetInvocationException e)
                {
                    throw new InvalidOperationException(
                        $"[PostConstruct] Error invoking {method.Name} on {type.Name}", e.InnerException ?? e);
                }
            }
        }

        public static void InvokeDeConstruct(object target)
        {
            if (target == null) return;

            var type = target.GetType();
            var methods = GetOrCacheMethods(type, DeConstructCache, typeof(DeConstructAttribute));

            foreach (var method in methods)
            {
                try
                {
                    method.Invoke(target, null);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(
                        $"[DeConstruct] Error invoking {method.Name} on {type.Name}: {e}");
                }
            }
        }

        private static MethodInfo[] GetOrCacheMethods(Type type, Dictionary<Type, MethodInfo[]> cache, Type attributeType)
        {
            if (cache.TryGetValue(type, out var methods))
                return methods;

            lock (_lock)
            {
                if (cache.TryGetValue(type, out methods))
                    return methods;

                methods = FindMethodsWithAttribute(type, attributeType);
                cache[type] = methods;
                return methods;
            }
        }

        private static MethodInfo[] FindMethodsWithAttribute(Type type, Type attributeType)
        {
            var result = new List<MethodInfo>();
            var methods = type.GetMethods(MethodFlags);

            foreach (var method in methods)
            {
                if (method.GetCustomAttribute(attributeType) != null && method.GetParameters().Length == 0)
                    result.Add(method);
            }

            return result.ToArray();
        }

        public static void ClearCache()
        {
            lock (_lock)
            {
                PostConstructCache.Clear();
                DeConstructCache.Clear();
            }
        }
    }
}
