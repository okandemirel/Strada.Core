using System;

namespace Strada.Core.DI.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false)]
    public sealed class InjectAttribute : Attribute
    {
    }
}
