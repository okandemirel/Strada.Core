using System;

namespace Strada.Core.DI
{
    public interface IContainer : IDisposable
    {
        T Resolve<T>() where T : class;
        object Resolve(Type type);
        bool TryResolve<T>(out T instance) where T : class;
        bool IsRegistered<T>() where T : class;
        bool IsRegistered(Type type);
        IContainerScope CreateScope();
    }

    public interface IContainerScope : IContainer
    {
        IContainer Parent { get; }
    }
}
