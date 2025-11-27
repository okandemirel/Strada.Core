using System;
using FsCheck;
using Strada.Core.DI;

namespace Strada.Core.Tests.Runtime.Generators
{
    /// <summary>
    /// Test interfaces and classes for DI container property testing.
    /// </summary>
    public interface ITestServiceA { }
    public interface ITestServiceB { }
    public interface ITestServiceC { }
    public interface ITestServiceD { }
    public interface ITestServiceE { }

    public class TestServiceA : ITestServiceA { }
    public class TestServiceB : ITestServiceB { }
    public class TestServiceC : ITestServiceC { }
    public class TestServiceD : ITestServiceD { }
    public class TestServiceE : ITestServiceE { }

    /// <summary>
    /// Disposable test service for testing container disposal.
    /// </summary>
    public class DisposableTestService : ITestServiceA, IDisposable
    {
        public bool IsDisposed { get; private set; }
        
        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    /// <summary>
    /// Service with a single dependency for testing injection.
    /// </summary>
    public class ServiceWithDependency : ITestServiceB
    {
        public ITestServiceA Dependency { get; }
        
        public ServiceWithDependency(ITestServiceA dependency)
        {
            Dependency = dependency;
        }
    }

    /// <summary>
    /// Service with multiple dependencies for testing injection completeness.
    /// </summary>
    public class ServiceWithMultipleDependencies : ITestServiceC
    {
        public ITestServiceA ServiceA { get; }
        public ITestServiceB ServiceB { get; }
        
        public ServiceWithMultipleDependencies(ITestServiceA serviceA, ITestServiceB serviceB)
        {
            ServiceA = serviceA;
            ServiceB = serviceB;
        }
    }

    /// <summary>
    /// Represents a registration configuration for property testing.
    /// </summary>
    public class RegistrationConfig
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public Lifetime Lifetime { get; }

        public RegistrationConfig(Type serviceType, Type implementationType, Lifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }

        public override string ToString() => 
            $"Registration({ServiceType.Name} -> {ImplementationType.Name}, {Lifetime})";
    }

    /// <summary>
    /// FsCheck generators for DI registration testing.
    /// </summary>
    public static class RegistrationGenerator
    {
        /// <summary>
        /// Available service type pairs for testing.
        /// </summary>
        private static readonly (Type Service, Type Implementation)[] ServicePairs = new[]
        {
            (typeof(ITestServiceA), typeof(TestServiceA)),
            (typeof(ITestServiceB), typeof(TestServiceB)),
            (typeof(ITestServiceC), typeof(TestServiceC)),
            (typeof(ITestServiceD), typeof(TestServiceD)),
            (typeof(ITestServiceE), typeof(TestServiceE)),
        };

        /// <summary>
        /// Generator for Lifetime enum values.
        /// </summary>
        public static Gen<Lifetime> LifetimeGen =>
            Gen.Elements(Lifetime.Singleton, Lifetime.Transient);

        /// <summary>
        /// Generator for a single registration configuration.
        /// </summary>
        public static Gen<RegistrationConfig> RegistrationConfigGen =>
            from pairIndex in Gen.Choose(0, ServicePairs.Length - 1)
            from lifetime in LifetimeGen
            let pair = ServicePairs[pairIndex]
            select new RegistrationConfig(pair.Service, pair.Implementation, lifetime);

        /// <summary>
        /// Generator for singleton registration configurations.
        /// </summary>
        public static Gen<RegistrationConfig> SingletonRegistrationGen =>
            from pairIndex in Gen.Choose(0, ServicePairs.Length - 1)
            let pair = ServicePairs[pairIndex]
            select new RegistrationConfig(pair.Service, pair.Implementation, Lifetime.Singleton);

        /// <summary>
        /// Generator for transient registration configurations.
        /// </summary>
        public static Gen<RegistrationConfig> TransientRegistrationGen =>
            from pairIndex in Gen.Choose(0, ServicePairs.Length - 1)
            let pair = ServicePairs[pairIndex]
            select new RegistrationConfig(pair.Service, pair.Implementation, Lifetime.Transient);

        /// <summary>
        /// Generator for a list of unique registration configurations.
        /// </summary>
        public static Gen<RegistrationConfig[]> UniqueRegistrations(int count)
        {
            count = Math.Min(count, ServicePairs.Length);
            return Gen.Shuffle(ServicePairs)
                .SelectMany(shuffled =>
                    Gen.ArrayOf(count, LifetimeGen)
                        .Select(lifetimes =>
                        {
                            var configs = new RegistrationConfig[count];
                            for (int i = 0; i < count; i++)
                            {
                                configs[i] = new RegistrationConfig(
                                    shuffled[i].Service,
                                    shuffled[i].Implementation,
                                    lifetimes[i]);
                            }
                            return configs;
                        }));
        }

        /// <summary>
        /// Generator for positive resolution count (1-10).
        /// </summary>
        public static Gen<int> ResolutionCountGen =>
            Gen.Choose(1, 10);

        /// <summary>
        /// Arbitrary instance for RegistrationConfig.
        /// </summary>
        public static Arbitrary<RegistrationConfig> RegistrationConfigArbitrary =>
            Arb.From(RegistrationConfigGen);

        /// <summary>
        /// Arbitrary instance for Lifetime.
        /// </summary>
        public static Arbitrary<Lifetime> LifetimeArbitrary =>
            Arb.From(LifetimeGen);
    }
}
