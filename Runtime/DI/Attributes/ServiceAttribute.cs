using System;

namespace Strada.Core.DI.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class ServiceAttribute : Attribute
    {
        public Lifetime Lifetime { get; }
        public Type InterfaceType { get; set; }

        public ServiceAttribute(Lifetime lifetime = Lifetime.Transient)
        {
            Lifetime = lifetime;
        }
    }
}
