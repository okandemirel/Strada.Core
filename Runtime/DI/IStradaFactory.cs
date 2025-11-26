using System;

namespace Strada.Core.DI
{
    public static class DirectFactory<T> where T : class
    {
        public static Func<IContainer, T> Delegate;
    }
}
