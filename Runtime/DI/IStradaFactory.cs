using System;

namespace Strada.Core.DI
{
    public interface IStradaFactory<T> where T : class
    {
        T Create(IContainer container);
    }

    public static class DirectFactory<T> where T : class
    {
        public static Func<IContainer, T> Delegate;
    }
}
