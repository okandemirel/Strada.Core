using System;

namespace Strada.Core.DI
{
    public interface IContainerBuilder
    {
        IContainerBuilder Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Singleton)
            where TInterface : class
            where TImplementation : class, TInterface;

        IContainerBuilder Register<T>(Lifetime lifetime = Lifetime.Singleton) where T : class;

        IContainerBuilder RegisterFactory<T>(Func<IContainer, T> factory, Lifetime lifetime = Lifetime.Transient)
            where T : class;

        IContainerBuilder RegisterInstance<T>(T instance) where T : class;

        IContainer Build();
    }
}
