using System;

namespace Strada.Core.DI.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AutoRegisterAttribute : Attribute
    {
        public Lifetime Lifetime { get; }
        public Type As { get; set; }
        public int Priority { get; set; }
        public bool RegisterSelf { get; set; }

        public AutoRegisterAttribute(Lifetime lifetime = Lifetime.Singleton)
        {
            Lifetime = lifetime;
        }
    }

    public abstract class AutoRegisterBaseAttribute : Attribute
    {
        public Type As { get; set; }
        public int Priority { get; set; }
        public bool RegisterSelf { get; set; }
        internal abstract Lifetime Lifetime { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AutoRegisterSingletonAttribute : AutoRegisterBaseAttribute
    {
        internal override Lifetime Lifetime => Lifetime.Singleton;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AutoRegisterTransientAttribute : AutoRegisterBaseAttribute
    {
        internal override Lifetime Lifetime => Lifetime.Transient;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AutoRegisterScopedAttribute : AutoRegisterBaseAttribute
    {
        internal override Lifetime Lifetime => Lifetime.Scoped;
    }
}
