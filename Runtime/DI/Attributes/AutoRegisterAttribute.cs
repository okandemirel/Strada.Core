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

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AutoRegisterSingletonAttribute : Attribute
    {
        public Type As { get; set; }
        public int Priority { get; set; }
        public bool RegisterSelf { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AutoRegisterTransientAttribute : Attribute
    {
        public Type As { get; set; }
        public int Priority { get; set; }
        public bool RegisterSelf { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AutoRegisterScopedAttribute : Attribute
    {
        public Type As { get; set; }
        public int Priority { get; set; }
        public bool RegisterSelf { get; set; }
    }
}
