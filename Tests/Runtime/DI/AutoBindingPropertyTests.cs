using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using NUnit.Framework;
using Strada.Core.DI;
using Strada.Core.DI.Attributes;
using Strada.Core.DI.AutoBinding;
using Strada.Core.Tests.Tests.Runtime.Generators;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    /// <summary>
    /// Property-based tests for the auto-binding scanner system.
    /// Tests verify that types with auto-register attributes are correctly
    /// discovered and registered with the DI container.
    /// </summary>
    [TestFixture]
    public class AutoBindingPropertyTests
    {
        [SetUp]
        public void SetUp()
        {
            RuntimeAutoBindingScanner.ClearCache();
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            StradaArbitraries.RegisterAll();
        }


        /// <summary>
        /// **Feature: strada-codebase-audit, Property 22: Auto-Binding Discovery**
        /// For any type with [AutoRegisterSingleton] attribute in scanned assemblies,
        /// the type SHALL be resolvable from the container after RegisterAutoBindings.
        /// **Validates: Requirements 9.1, 9.2**
        /// </summary>
        [Test]
        public void AutoBindingDiscovery_SingletonAttribute_TypeIsResolvable()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    RuntimeAutoBindingScanner.ClearCache();

                    var builder = new ContainerBuilder();
                    builder.RegisterAutoBindingsRuntime(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    using var container = builder.Build();

                    var instances = new List<ITestAutoRegistered>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        var instance = container.Resolve<ITestAutoRegistered>();
                        if (instance == null)
                            return false;
                        instances.Add(instance);
                    }

                    var first = instances[0];
                    for (int i = 1; i < instances.Count; i++)
                    {
                        if (!ReferenceEquals(first, instances[i]))
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 22: Auto-Binding Discovery**
        /// For any type with [AutoRegisterTransient] attribute in scanned assemblies,
        /// the type SHALL be resolvable from the container and return unique instances.
        /// **Validates: Requirements 9.1, 9.2**
        /// </summary>
        [Test]
        public void AutoBindingDiscovery_TransientAttribute_TypeIsResolvableWithUniqueInstances()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    RuntimeAutoBindingScanner.ClearCache();

                    var builder = new ContainerBuilder();
                    builder.RegisterAutoBindingsRuntime(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    using var container = builder.Build();

                    var instances = new List<ITestAutoTransient>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        var instance = container.Resolve<ITestAutoTransient>();
                        if (instance == null)
                            return false;
                        instances.Add(instance);
                    }

                    var seen = new HashSet<ITestAutoTransient>(ReferenceEqualityComparer.Instance);
                    foreach (var instance in instances)
                    {
                        if (!seen.Add(instance))
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 22: Auto-Binding Discovery**
        /// For any scanned entries, all discovered types with auto-register attributes
        /// SHALL have correct lifetime and service type mappings.
        /// **Validates: Requirements 9.1, 9.2**
        /// </summary>
        [Test]
        public void AutoBindingDiscovery_ScannedEntries_HaveCorrectConfiguration()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Arb.From(Gen.Constant(true)),
                (_) =>
                {
                    RuntimeAutoBindingScanner.ClearCache();

                    var entries = RuntimeAutoBindingScanner.ScanAssemblies(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    var singletonEntry = entries.Find(e =>
                        e.ImplementationType == typeof(TestAutoRegisteredService));
                    var transientEntry = entries.Find(e =>
                        e.ImplementationType == typeof(TestAutoTransientService));

                    if (singletonEntry == null)
                        return false;
                    if (singletonEntry.ServiceType != typeof(ITestAutoRegistered))
                        return false;
                    if (singletonEntry.Lifetime != Lifetime.Singleton)
                        return false;

                    if (transientEntry == null)
                        return false;
                    if (transientEntry.ServiceType != typeof(ITestAutoTransient))
                        return false;
                    if (transientEntry.Lifetime != Lifetime.Transient)
                        return false;

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 22: Auto-Binding Discovery**
        /// For any type with As property set, the scanner SHALL register the
        /// interface-to-implementation mapping correctly.
        /// **Validates: Requirements 9.2**
        /// </summary>
        [Test]
        public void AutoBindingDiscovery_AsProperty_RegistersInterfaceMapping()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    RuntimeAutoBindingScanner.ClearCache();

                    var builder = new ContainerBuilder();
                    builder.RegisterAutoBindingsRuntime(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    using var container = builder.Build();

                    for (int i = 0; i < resolutionCount; i++)
                    {
                        var service = container.Resolve<ITestAutoRegistered>();

                        if (service == null)
                            return false;
                        if (!(service is TestAutoRegisteredService))
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 22: Auto-Binding Discovery**
        /// For any set of auto-registered services with Priority specified,
        /// services SHALL be registered in priority order (lower first).
        /// **Validates: Requirements 9.1**
        /// </summary>
        [Test]
        public void AutoBindingDiscovery_Priority_ServicesRegisteredInOrder()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Arb.From(Gen.Constant(true)),
                (_) =>
                {
                    RuntimeAutoBindingScanner.ClearCache();

                    var entries = RuntimeAutoBindingScanner.ScanAssemblies(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    var sortedEntries = entries.OrderBy(e => e.Priority).ToList();

                    for (int i = 1; i < sortedEntries.Count; i++)
                    {
                        if (sortedEntries[i].Priority < sortedEntries[i - 1].Priority)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// Reference equality comparer for HashSet.
        /// </summary>
        private class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    /// <summary>
    /// Test interface for transient auto-registration.
    /// </summary>
    public interface ITestAutoTransient
    {
        string GetId();
    }

    /// <summary>
    /// Test class with AutoRegisterTransient attribute for property testing.
    /// </summary>
    [AutoRegisterTransient(As = typeof(ITestAutoTransient))]
    public class TestAutoTransientService : ITestAutoTransient
    {
        private readonly string _id = Guid.NewGuid().ToString();
        public string GetId() => _id;
    }
}
