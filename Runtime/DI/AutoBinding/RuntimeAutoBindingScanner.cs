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
            lock (_lock)
            {
                if (_cachedEntries != null)
                    return _cachedEntries;
            }

            includePatterns ??= new[] { "Strada.*", "Game.*", "Assembly-CSharp" };
            excludePatterns ??= new[] { "Unity.*", "System.*", "Mono.*", "mscorlib", "*.Tests", "*.Editor" };

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
                catch (ReflectionTypeLoadException)
                {
                }
            }

            lock (_lock)
            {
                _cachedEntries = entries;
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

            var stradaService = type.GetCustomAttribute<StradaServiceAttribute>();
            if (stradaService != null)
            {
                return new AutoBindingEntry
                {
                    ImplementationType = type,
                    ServiceType = stradaService.InterfaceType ?? type,
                    Lifetime = stradaService.Lifetime,
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
                return name.Contains(pattern.Trim('*'));
            if (pattern.StartsWith("*"))
                return name.EndsWith(pattern.TrimStart('*'));
            if (pattern.EndsWith("*"))
                return name.StartsWith(pattern.TrimEnd('*'));
            return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        public static void ClearCache()
        {
            lock (_lock)
            {
                _cachedEntries = null;
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
