using System;

namespace Strada.Core.DI.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class StradaServiceAttribute : Attribute
    {
        public Lifetime Lifetime { get; }
        public Type InterfaceType { get; set; }

        public StradaServiceAttribute(Lifetime lifetime = Lifetime.Transient)
        {
            Lifetime = lifetime;
        }
    }
}
