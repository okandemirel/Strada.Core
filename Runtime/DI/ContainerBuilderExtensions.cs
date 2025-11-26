using System;
using System.Collections.Generic;
using Strada.Core.DI.AutoBinding;

namespace Strada.Core.DI
{
    public static class ContainerBuilderExtensions
    {
        public static IContainerBuilder RegisterAutoBindings(this IContainerBuilder builder)
        {
            return RegisterAutoBindings(builder, null, null, false);
        }

        public static IContainerBuilder RegisterAutoBindings(
            this IContainerBuilder builder,
            IReadOnlyList<string> includePatterns,
            IReadOnlyList<string> excludePatterns,
            bool forceRuntime = false)
        {
            if (!forceRuntime && TryUseSourceGenerated(builder))
                return builder;

            RuntimeAutoBindingScanner.RegisterAll(builder, includePatterns, excludePatterns);
            return builder;
        }

        public static IContainerBuilder RegisterAutoBindingsRuntime(
            this IContainerBuilder builder,
            IReadOnlyList<string> includePatterns = null,
            IReadOnlyList<string> excludePatterns = null)
        {
            RuntimeAutoBindingScanner.RegisterAll(builder, includePatterns, excludePatterns);
            return builder;
        }

        private static bool TryUseSourceGenerated(IContainerBuilder builder)
        {
            try
            {
                var registryType =
                    Type.GetType("Strada.Generated.StradaGeneratedRegistry, Assembly-CSharp") ??
                    Type.GetType("Strada.Generated.StradaGeneratedRegistry, Assembly-CSharp-firstpass") ??
                    Type.GetType("Strada.Generated.StradaGeneratedRegistry");

                if (registryType == null)
                    return false;

                var isGenProp = registryType.GetProperty("IsSourceGenerated");
                if (isGenProp != null && !(bool)isGenProp.GetValue(null))
                    return false;

                var registerMethod = registryType.GetMethod("RegisterAll");
                if (registerMethod == null)
                    return false;

                registerMethod.Invoke(null, new object[] { builder });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int GetAutoBindingCount()
        {
            try
            {
                var registryType =
                    Type.GetType("Strada.Generated.StradaGeneratedRegistry, Assembly-CSharp") ??
                    Type.GetType("Strada.Generated.StradaGeneratedRegistry");

                if (registryType != null)
                {
                    var countProp = registryType.GetProperty("ServiceCount");
                    if (countProp != null)
                        return (int)countProp.GetValue(null);
                }
            }
            catch { }

            return RuntimeAutoBindingScanner.GetCachedCount();
        }
    }
}
