using System;

namespace Strada.Core.DI.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class StradaServiceAttribute : Attribute
    {
        public ServiceLifetime Lifetime { get; }
        public Type? InterfaceType { get; set; }

        public StradaServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            Lifetime = lifetime;
        }
    }

    public enum ServiceLifetime
    {
        Transient,
        Singleton,
        Scoped
    }
}
