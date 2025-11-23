using System;

namespace Strada.Core.DI
{
    /// <summary>
    /// Internal class representing a type registration entry.
    /// </summary>
    internal class Registration
    {
        /// <summary>
        /// The type that was registered (interface or concrete type).
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// The implementation type to instantiate.
        /// </summary>
        public Type ImplementationType { get; }

        /// <summary>
        /// The lifetime of the registration.
        /// </summary>
        public Lifetime Lifetime { get; }

        /// <summary>
        /// Optional factory function for creating instances.
        /// </summary>
        public Func<IContainer, object> Factory { get; }

        /// <summary>
        /// Pre-created instance for RegisterInstance.
        /// </summary>
        public object Instance { get; }

        /// <summary>
        /// Cached constructor info for performance.
        /// </summary>
        public System.Reflection.ConstructorInfo Constructor { get; set; }

        /// <summary>
        /// Creates a registration for a type with implementation.
        /// </summary>
        public Registration(Type serviceType, Type implementationType, Lifetime lifetime)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
            Lifetime = lifetime;
            Factory = null;
            Instance = null;
        }

        /// <summary>
        /// Creates a registration with a factory function.
        /// </summary>
        public Registration(Type serviceType, Func<IContainer, object> factory, Lifetime lifetime)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Lifetime = lifetime;
            ImplementationType = null;
            Instance = null;
        }

        /// <summary>
        /// Creates a registration for a pre-created instance.
        /// </summary>
        public Registration(Type serviceType, object instance)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            Lifetime = Lifetime.Singleton; // Instances are always singletons
            ImplementationType = instance.GetType();
            Factory = null;
        }

        /// <summary>
        /// Validates that the implementation type is assignable to the service type.
        /// </summary>
        public void Validate()
        {
            if (Instance != null)
            {
                if (!ServiceType.IsAssignableFrom(Instance.GetType()))
                {
                    throw new InvalidOperationException(
                        $"Instance of type '{Instance.GetType().Name}' is not assignable to service type '{ServiceType.Name}'");
                }
                return;
            }

            if (Factory != null)
            {
                return;
            }

            if (ImplementationType != null)
            {
                if (!ServiceType.IsAssignableFrom(ImplementationType))
                {
                    throw new InvalidOperationException(
                        $"Implementation type '{ImplementationType.Name}' is not assignable to service type '{ServiceType.Name}'");
                }

                if (ImplementationType.IsAbstract || ImplementationType.IsInterface)
                {
                    throw new InvalidOperationException(
                        $"Implementation type '{ImplementationType.Name}' cannot be abstract or an interface");
                }
            }
        }
    }
}
