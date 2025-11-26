using System;

namespace Strada.Core.DI
{
    public enum Lifetime
    {
        Transient = 0,
        Singleton = 1,
        Scoped = 2
    }

    internal sealed class Registration
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public Lifetime Lifetime { get; }
        public Func<IContainer, object> Factory { get; }
        public object Instance { get; }

        private Registration(Type serviceType, Type implementationType, Lifetime lifetime,
            Func<IContainer, object> factory, object instance)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
            Factory = factory;
            Instance = instance;
        }

        public static Registration FromType(Type serviceType, Type implementationType, Lifetime lifetime)
        {
            return new Registration(serviceType, implementationType, lifetime, null, null);
        }

        public static Registration FromFactory(Type serviceType, Func<IContainer, object> factory, Lifetime lifetime)
        {
            return new Registration(serviceType, null, lifetime, factory, null);
        }

        public static Registration FromInstance(Type serviceType, object instance)
        {
            return new Registration(serviceType, instance.GetType(), Lifetime.Singleton, null, instance);
        }
    }
}
