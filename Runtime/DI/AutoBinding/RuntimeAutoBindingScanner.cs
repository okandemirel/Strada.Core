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

            foreach (var entry in entries.OrderBy(e => e.Priority))
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

            var singleton = type.GetCustomAttribute<AutoRegisterSingletonAttribute>();
            if (singleton != null)
            {
                return new AutoBindingEntry
                {
                    ImplementationType = type,
                    ServiceType = singleton.As ?? type,
                    Lifetime = Lifetime.Singleton,
                    Priority = singleton.Priority,
                    RegisterSelf = singleton.RegisterSelf
                };
            }

            var transient = type.GetCustomAttribute<AutoRegisterTransientAttribute>();
            if (transient != null)
            {
                return new AutoBindingEntry
                {
                    ImplementationType = type,
                    ServiceType = transient.As ?? type,
                    Lifetime = Lifetime.Transient,
                    Priority = transient.Priority,
                    RegisterSelf = transient.RegisterSelf
                };
            }

            var scoped = type.GetCustomAttribute<AutoRegisterScopedAttribute>();
            if (scoped != null)
            {
                return new AutoBindingEntry
                {
                    ImplementationType = type,
                    ServiceType = scoped.As ?? type,
                    Lifetime = Lifetime.Scoped,
                    Priority = scoped.Priority,
                    RegisterSelf = scoped.RegisterSelf
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

        private static void RegisterEntry(IContainerBuilder builder, AutoBindingEntry entry)
        {
            var builderType = typeof(IContainerBuilder);

            if (entry.ServiceType != entry.ImplementationType)
            {
                if (!entry.ServiceType.IsAssignableFrom(entry.ImplementationType))
                {
                    UnityEngine.Debug.LogWarning(
                        $"AutoBinding skipped: {entry.ImplementationType.FullName} is not assignable to {entry.ServiceType.FullName}");
                    return;
                }

                var method = builderType.GetMethods()
                    .First(m => m.Name == "Register" &&
                                m.GetGenericArguments().Length == 2 &&
                                m.GetParameters().Length == 1);
                var generic = method.MakeGenericMethod(entry.ServiceType, entry.ImplementationType);
                generic.Invoke(builder, new object[] { entry.Lifetime });

                if (entry.RegisterSelf)
                {
                    var selfMethod = builderType.GetMethods()
                        .First(m => m.Name == "Register" &&
                                    m.GetGenericArguments().Length == 1 &&
                                    m.GetParameters().Length == 1);
                    var selfGeneric = selfMethod.MakeGenericMethod(entry.ImplementationType);
                    selfGeneric.Invoke(builder, new object[] { entry.Lifetime });
                }
            }
            else
            {
                var method = builderType.GetMethods()
                    .First(m => m.Name == "Register" &&
                                m.GetGenericArguments().Length == 1 &&
                                m.GetParameters().Length == 1);
                var generic = method.MakeGenericMethod(entry.ImplementationType);
                generic.Invoke(builder, new object[] { entry.Lifetime });
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
