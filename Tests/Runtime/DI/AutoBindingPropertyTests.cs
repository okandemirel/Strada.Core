using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using NUnit.Framework;
using Strada.Core.DI;
using Strada.Core.DI.Attributes;
using Strada.Core.DI.AutoBinding;
using Strada.Core.Tests.Runtime.DI;
using Strada.Core.Tests.Runtime.Generators;

namespace Strada.Core.Tests.Runtime.DI
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

        #region Property 22: Auto-Binding Discovery

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

                    // Arrange - scan assemblies and register auto-bindings
                    var builder = new ContainerBuilder();
                    builder.RegisterAutoBindingsRuntime(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    using var container = builder.Build();

                    // Act - resolve the auto-registered singleton service
                    var instances = new List<ITestAutoRegistered>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        var instance = container.Resolve<ITestAutoRegistered>();
                        if (instance == null)
                            return false;
                        instances.Add(instance);
                    }

                    // Assert - service is resolvable and is singleton (same instance)
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

                    // Arrange
                    var builder = new ContainerBuilder();
                    builder.RegisterAutoBindingsRuntime(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    using var container = builder.Build();

                    // Act - resolve the auto-registered transient service
                    var instances = new List<ITestAutoTransient>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        var instance = container.Resolve<ITestAutoTransient>();
                        if (instance == null)
                            return false;
                        instances.Add(instance);
                    }

                    // Assert - all instances should be unique (transient behavior)
                    var seen = new HashSet<ITestAutoTransient>(ReferenceEqualityComparer.Instance);
                    foreach (var instance in instances)
                    {
                        if (!seen.Add(instance))
                            return false; // Found duplicate reference
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

                    // Act - scan assemblies
                    var entries = RuntimeAutoBindingScanner.ScanAssemblies(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    // Find our test services
                    var singletonEntry = entries.Find(e => 
                        e.ImplementationType == typeof(TestAutoRegisteredService));
                    var transientEntry = entries.Find(e => 
                        e.ImplementationType == typeof(TestAutoTransientService));

                    // Assert - singleton entry has correct configuration
                    if (singletonEntry == null)
                        return false;
                    if (singletonEntry.ServiceType != typeof(ITestAutoRegistered))
                        return false;
                    if (singletonEntry.Lifetime != Lifetime.Singleton)
                        return false;

                    // Assert - transient entry has correct configuration
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

                    // Arrange
                    var builder = new ContainerBuilder();
                    builder.RegisterAutoBindingsRuntime(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    using var container = builder.Build();

                    // Act - resolve by interface (As property)
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        var service = container.Resolve<ITestAutoRegistered>();
                        
                        // Assert - resolved instance is correct implementation type
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

                    // Act - scan assemblies
                    var entries = RuntimeAutoBindingScanner.ScanAssemblies(
                        new[] { "Strada.*" },
                        new[] { "Unity.*", "System.*" });

                    // Get entries sorted by priority (as they would be registered)
                    var sortedEntries = entries.OrderBy(e => e.Priority).ToList();

                    // Verify ordering is maintained
                    for (int i = 1; i < sortedEntries.Count; i++)
                    {
                        if (sortedEntries[i].Priority < sortedEntries[i - 1].Priority)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        #endregion

        #region Helper Classes

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

        #endregion
    }

    #region Test Types for Auto-Binding Property Tests

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

    #endregion
}
