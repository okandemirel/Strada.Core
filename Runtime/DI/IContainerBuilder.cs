using System;

namespace Strada.Core.DI
{
    /// <summary>
    /// Builder for configuring and creating a dependency injection container.
    /// </summary>
    public interface IContainerBuilder
    {
        /// <summary>
        /// Registers a type with its implementation.
        /// </summary>
        /// <typeparam name="TInterface">The interface type to register.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <param name="lifetime">The lifetime of the registration.</param>
        /// <returns>The builder for method chaining.</returns>
        IContainerBuilder Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Transient)
            where TInterface : class
            where TImplementation : class, TInterface;

        /// <summary>
        /// Registers a concrete type.
        /// </summary>
        /// <typeparam name="T">The concrete type to register.</typeparam>
        /// <param name="lifetime">The lifetime of the registration.</param>
        /// <returns>The builder for method chaining.</returns>
        IContainerBuilder Register<T>(Lifetime lifetime = Lifetime.Transient) where T : class;

        /// <summary>
        /// Registers a type with a factory function.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="factory">Factory function to create instances.</param>
        /// <param name="lifetime">The lifetime of the registration.</param>
        /// <returns>The builder for method chaining.</returns>
        IContainerBuilder RegisterFactory<T>(Func<IContainer, T> factory, Lifetime lifetime = Lifetime.Transient)
            where T : class;

        /// <summary>
        /// Registers a specific instance (always singleton).
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="instance">The instance to register.</param>
        /// <returns>The builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
        /// <remarks>
        /// The instance is registered with its runtime type.
        /// Use Register&lt;TInterface, TImplementation&gt; if you need interface-based registration.
        /// </remarks>
        IContainerBuilder RegisterInstance<T>(T instance) where T : class;

        /// <summary>
        /// Builds the immutable container from registered types.
        /// </summary>
        /// <returns>The configured container.</returns>
        /// <exception cref="InvalidOperationException">Thrown when there are circular dependencies.</exception>
        IContainer Build();
    }

    /// <summary>
    /// Defines the lifetime of a registered type.
    /// </summary>
    public enum Lifetime
    {
        /// <summary>
        /// A new instance is created for each resolution.
        /// </summary>
        Transient,

        /// <summary>
        /// A single instance is shared across the entire container and all scopes.
        /// </summary>
        Singleton,

        /// <summary>
        /// A single instance is shared within a scope. Different scopes have different instances.
        /// </summary>
        Scoped
    }
}
