using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Strada.Core.DI.Attributes;

namespace Strada.Core.DI.AutoBinding
{
    public sealed class AutoBindingEntry
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public Lifetime Lifetime { get; set; }
        public int Priority { get; set; }
        public bool RegisterSelf { get; set; }
    }

    public static class RuntimeAutoBindingScanner
    {
        private static List<AutoBindingEntry> _cachedEntries;
        private static string _cachedKey;
        private static readonly object _lock = new();

        public static void RegisterAll(
            IContainerBuilder builder,
            IReadOnlyList<string> includePatterns = null,
            IReadOnlyList<string> excludePatterns = null)
        {
            var entries = ScanAssemblies(includePatterns, excludePatterns);
            var sorted = new List<AutoBindingEntry>(entries);
            sorted.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            foreach (var entry in sorted)
            {
                RegisterEntry(builder, entry);
            }
        }

        public static List<AutoBindingEntry> ScanAssemblies(
            IReadOnlyList<string> includePatterns = null,
            IReadOnlyList<string> excludePatterns = null)
        {
            includePatterns ??= new[] { "Strada.*", "Game.*", "Assembly-CSharp" };
            excludePatterns ??= new[] { "Unity.*", "System.*", "Mono.*", "mscorlib", "*.Tests", "*.Editor" };

            var key = BuildCacheKey(includePatterns, excludePatterns);

            lock (_lock)
            {
                if (_cachedEntries != null && _cachedKey == key)
                    return _cachedEntries;
            }

            var entries = new List<AutoBindingEntry>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                var name = assembly.GetName().Name;
                if (!MatchesAnyPattern(name, includePatterns) ||
                    MatchesAnyPattern(name, excludePatterns))
                    continue;

                try
                {
                    ScanAssembly(assembly, entries);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    UnityEngine.Debug.LogWarning($"Partial type load from assembly {assembly.GetName().Name}: {ex.Message}");
                    var loadedTypes = ex.Types;
                    if (loadedTypes != null)
                    {
                        foreach (var type in loadedTypes)
                        {
                            if (type == null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                                continue;
                            var entry = TryCreateEntry(type);
                            if (entry != null)
                                entries.Add(entry);
                        }
                    }
                }
            }

            lock (_lock)
            {
                _cachedEntries = entries;
                _cachedKey = key;
            }

            return entries;
        }

        private static void ScanAssembly(Assembly assembly, List<AutoBindingEntry> entries)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;

                var entry = TryCreateEntry(type);
                if (entry != null)
                    entries.Add(entry);
            }
        }

        private static AutoBindingEntry TryCreateEntry(Type type)
        {
            var autoReg = type.GetCustomAttribute<AutoRegisterAttribute>();
            if (autoReg != null)
            {
                return new AutoBindingEntry
                {
                    ImplementationType = type,
                    ServiceType = autoReg.As ?? type,
                    Lifetime = autoReg.Lifetime,
                    Priority = autoReg.Priority,
                    RegisterSelf = autoReg.RegisterSelf
                };
            }

            var baseAttr = type.GetCustomAttribute<AutoRegisterBaseAttribute>(inherit: false);
            if (baseAttr != null)
            {
                return new AutoBindingEntry
                {
                    ImplementationType = type,
                    ServiceType = baseAttr.As ?? type,
                    Lifetime = baseAttr.Lifetime,
                    Priority = baseAttr.Priority,
                    RegisterSelf = baseAttr.RegisterSelf
                };
            }

            var serviceAttr = type.GetCustomAttribute<ServiceAttribute>();
            if (serviceAttr != null)
            {
                return new AutoBindingEntry
                {
                    ImplementationType = type,
                    ServiceType = serviceAttr.InterfaceType ?? type,
                    Lifetime = serviceAttr.Lifetime,
                    Priority = 0,
                    RegisterSelf = false
                };
            }

            return null;
        }

        private static MethodInfo _registerOneGeneric;
        private static MethodInfo _registerTwoGeneric;

        private static MethodInfo RegisterOneGeneric =>
            _registerOneGeneric ??= Array.Find(typeof(IContainerBuilder).GetMethods(),
                m => m.Name == "Register" && m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 1);

        private static MethodInfo RegisterTwoGeneric =>
            _registerTwoGeneric ??= Array.Find(typeof(IContainerBuilder).GetMethods(),
                m => m.Name == "Register" && m.GetGenericArguments().Length == 2 && m.GetParameters().Length == 1);

        private static void RegisterEntry(IContainerBuilder builder, AutoBindingEntry entry)
        {
            var args = new object[] { entry.Lifetime };

            if (entry.ServiceType != entry.ImplementationType)
            {
                if (!entry.ServiceType.IsAssignableFrom(entry.ImplementationType))
                {
                    UnityEngine.Debug.LogWarning(
                        $"AutoBinding skipped: {entry.ImplementationType.FullName} is not assignable to {entry.ServiceType.FullName}");
                    return;
                }

                RegisterTwoGeneric.MakeGenericMethod(entry.ServiceType, entry.ImplementationType)
                    .Invoke(builder, args);

                if (entry.RegisterSelf)
                {
                    RegisterOneGeneric.MakeGenericMethod(entry.ImplementationType)
                        .Invoke(builder, args);
                }
            }
            else
            {
                RegisterOneGeneric.MakeGenericMethod(entry.ImplementationType)
                    .Invoke(builder, args);
            }
        }

        private static bool MatchesAnyPattern(string name, IReadOnlyList<string> patterns)
        {
            foreach (var pattern in patterns)
            {
                if (MatchesPattern(name, pattern))
                    return true;
            }
            return false;
        }

        private static bool MatchesPattern(string name, string pattern)
        {
            if (pattern.StartsWith("*") && pattern.EndsWith("*"))
                return name.Contains(pattern.Trim('*'), StringComparison.OrdinalIgnoreCase);
            if (pattern.StartsWith("*"))
                return name.EndsWith(pattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase);
            if (pattern.EndsWith("*"))
                return name.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase);
            return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCacheKey(IReadOnlyList<string> includePatterns, IReadOnlyList<string> excludePatterns)
        {
            var includes = includePatterns.OrderBy(p => p, StringComparer.Ordinal);
            var excludes = excludePatterns.OrderBy(p => p, StringComparer.Ordinal);
            return string.Join("|", includes) + "||" + string.Join("|", excludes);
        }

        public static void ClearCache()
        {
            lock (_lock)
            {
                _cachedEntries = null;
                _cachedKey = null;
            }
        }

        public static int GetCachedCount()
        {
            lock (_lock)
            {
                return _cachedEntries?.Count ?? 0;
            }
        }
    }
}
