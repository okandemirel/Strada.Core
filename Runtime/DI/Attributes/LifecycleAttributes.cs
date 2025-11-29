using System;

namespace Strada.Core.DI.Attributes
{
    /// <summary>
    /// Marks a method to be called after the object is fully constructed and injected.
    /// Use for initialization logic that depends on injected dependencies.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PostConstructAttribute : Attribute { }

    /// <summary>
    /// Marks a method to be called when the object is being disposed/destroyed.
    /// Use for cleanup logic like unsubscribing from events or releasing resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class DeConstructAttribute : Attribute { }
}
