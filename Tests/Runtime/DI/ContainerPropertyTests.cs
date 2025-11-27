using System;
using System.Collections.Generic;
using FsCheck;
using NUnit.Framework;
using Strada.Core.DI;
using Strada.Core.Tests.Tests.Runtime.Generators;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    /// <summary>
    /// Property-based tests for the DI container.
    /// Tests verify correctness properties that must hold across all valid inputs.
    /// </summary>
    [TestFixture]
    public class ContainerPropertyTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            StradaArbitraries.RegisterAll();
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 1: Singleton Identity**
        /// For any registered singleton service and any number of resolution calls,
        /// all returned instances SHALL be reference-equal.
        /// **Validates: Requirements 2.1**
        /// </summary>
        [Test]
        public void SingletonIdentity_AllResolutionsReturnSameInstance()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ITestServiceA, TestServiceA>(Lifetime.Singleton);
                    using var container = builder.Build();

                    var instances = new List<ITestServiceA>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        instances.Add(container.Resolve<ITestServiceA>());
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
        /// **Feature: strada-codebase-audit, Property 1: Singleton Identity**
        /// Additional test: Factory-registered singletons also maintain identity.
        /// **Validates: Requirements 2.1**
        /// </summary>
        [Test]
        public void SingletonIdentity_FactoryRegisteredSingleton_ReturnsSameInstance()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    var builder = new ContainerBuilder();
                    builder.RegisterFactory<ITestServiceA>(c => new TestServiceA(), Lifetime.Singleton);
                    using var container = builder.Build();

                    var instances = new List<ITestServiceA>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        instances.Add(container.Resolve<ITestServiceA>());
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
        /// **Feature: strada-codebase-audit, Property 2: Transient Uniqueness**
        /// For any registered transient service and any two resolution calls,
        /// the returned instances SHALL NOT be reference-equal.
        /// **Validates: Requirements 2.2**
        /// </summary>
        [Test]
        public void TransientUniqueness_EachResolutionReturnsNewInstance()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ITestServiceA, TestServiceA>(Lifetime.Transient);
                    using var container = builder.Build();

                    var instances = new List<ITestServiceA>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        instances.Add(container.Resolve<ITestServiceA>());
                    }

                    var seen = new HashSet<ITestServiceA>(ReferenceEqualityComparer.Instance);
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
        /// **Feature: strada-codebase-audit, Property 2: Transient Uniqueness**
        /// Additional test: Factory-registered transients also produce unique instances.
        /// **Validates: Requirements 2.2**
        /// </summary>
        [Test]
        public void TransientUniqueness_FactoryRegisteredTransient_ReturnsNewInstances()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    var builder = new ContainerBuilder();
                    builder.RegisterFactory<ITestServiceA>(c => new TestServiceA(), Lifetime.Transient);
                    using var container = builder.Build();

                    var instances = new List<ITestServiceA>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        instances.Add(container.Resolve<ITestServiceA>());
                    }

                    var seen = new HashSet<ITestServiceA>(ReferenceEqualityComparer.Instance);
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
        /// **Feature: strada-codebase-audit, Property 3: Dependency Injection Completeness**
        /// For any service with N constructor dependencies, after resolution
        /// all N dependencies SHALL be non-null and of correct type.
        /// **Validates: Requirements 2.4**
        /// </summary>
        [Test]
        public void DependencyInjectionCompleteness_SingleDependency_IsInjected()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.LifetimeGen.ToArbitrary(),
                (lifetime) =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ITestServiceA, TestServiceA>(lifetime);
                    builder.Register<ITestServiceB, ServiceWithDependency>(lifetime);
                    using var container = builder.Build();

                    var service = container.Resolve<ITestServiceB>() as ServiceWithDependency;

                    return service != null &&
                           service.Dependency != null &&
                           service.Dependency is ITestServiceA;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 3: Dependency Injection Completeness**
        /// For services with multiple dependencies, all are injected correctly.
        /// **Validates: Requirements 2.4**
        /// </summary>
        [Test]
        public void DependencyInjectionCompleteness_MultipleDependencies_AllInjected()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.LifetimeGen.ToArbitrary(),
                (lifetime) =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ITestServiceA, TestServiceA>(lifetime);
                    builder.Register<ITestServiceB, ServiceWithDependency>(lifetime);
                    builder.Register<ITestServiceC, ServiceWithMultipleDependencies>(lifetime);
                    using var container = builder.Build();

                    var service = container.Resolve<ITestServiceC>() as ServiceWithMultipleDependencies;

                    return service != null &&
                           service.ServiceA != null &&
                           service.ServiceA is ITestServiceA &&
                           service.ServiceB != null &&
                           service.ServiceB is ITestServiceB;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 3: Dependency Injection Completeness**
        /// Singleton dependencies are shared across dependent services.
        /// **Validates: Requirements 2.4**
        /// </summary>
        [Test]
        public void DependencyInjectionCompleteness_SingletonDependency_IsShared()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ITestServiceA, TestServiceA>(Lifetime.Singleton);
                    builder.Register<ITestServiceB, ServiceWithDependency>(Lifetime.Transient);
                    using var container = builder.Build();

                    var services = new List<ServiceWithDependency>();
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        services.Add(container.Resolve<ITestServiceB>() as ServiceWithDependency);
                    }

                    var firstDep = services[0].Dependency;
                    for (int i = 1; i < services.Count; i++)
                    {
                        if (!ReferenceEquals(firstDep, services[i].Dependency))
                            return false;
                    }
                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 4: Disposable Singleton Cleanup**
        /// For any set of singleton services implementing IDisposable,
        /// after container disposal all SHALL have IsDisposed=true.
        /// **Validates: Requirements 2.5**
        /// </summary>
        [Test]
        public void DisposableSingletonCleanup_SingleDisposable_IsDisposed()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                RegistrationGenerator.ResolutionCountGen.ToArbitrary(),
                (resolutionCount) =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ITestServiceA, DisposableTestService>(Lifetime.Singleton);
                    var container = builder.Build();

                    DisposableTestService instance = null;
                    for (int i = 0; i < resolutionCount; i++)
                    {
                        instance = container.Resolve<ITestServiceA>() as DisposableTestService;
                    }

                    container.Dispose();

                    return instance != null && instance.IsDisposed;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 4: Disposable Singleton Cleanup**
        /// Multiple disposable singletons are all disposed.
        /// **Validates: Requirements 2.5**
        /// </summary>
        [Test]
        public void DisposableSingletonCleanup_MultipleDisposables_AllDisposed()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Arb.From(Gen.Constant(true)), // Just need to run the test
                (_) =>
                {
                    var builder = new ContainerBuilder();
                    builder.RegisterFactory<ITestServiceA>(c => new DisposableTestService(), Lifetime.Singleton);
                    builder.RegisterFactory<ITestServiceB>(c => new DisposableServiceB(), Lifetime.Singleton);
                    var container = builder.Build();

                    var serviceA = container.Resolve<ITestServiceA>() as DisposableTestService;
                    var serviceB = container.Resolve<ITestServiceB>() as DisposableServiceB;

                    container.Dispose();

                    return serviceA != null && serviceA.IsDisposed &&
                           serviceB != null && serviceB.IsDisposed;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 4: Disposable Singleton Cleanup**
        /// Non-disposable singletons don't cause issues during disposal.
        /// **Validates: Requirements 2.5**
        /// </summary>
        [Test]
        public void DisposableSingletonCleanup_MixedDisposableAndNonDisposable_OnlyDisposablesDisposed()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Arb.From(Gen.Constant(true)),
                (_) =>
                {
                    var builder = new ContainerBuilder();
                    builder.Register<ITestServiceA, DisposableTestService>(Lifetime.Singleton);
                    builder.Register<ITestServiceB, TestServiceB>(Lifetime.Singleton); // Non-disposable
                    var container = builder.Build();

                    var disposable = container.Resolve<ITestServiceA>() as DisposableTestService;
                    var nonDisposable = container.Resolve<ITestServiceB>();

                    container.Dispose();

                    return disposable != null && disposable.IsDisposed && nonDisposable != null;
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
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// Additional disposable service for multi-disposable tests.
        /// </summary>
        private class DisposableServiceB : ITestServiceB, IDisposable
        {
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }
    }
}
