using System;

namespace Strada.Core.DI
{
    /// <summary>
    /// Represents a dependency injection container for resolving registered services.
    /// </summary>
    /// <remarks>
    /// The container is immutable after being built from a <see cref="IContainerBuilder"/>.
    /// This ensures thread-safety and prevents runtime modifications.
    /// </remarks>
    public interface IContainer : IDisposable
    {
        /// <summary>
        /// Resolves an instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <returns>An instance of the requested type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the type is not registered.</exception>
        T Resolve<T>() where T : class;

        /// <summary>
        /// Resolves an instance of the specified type.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <returns>An instance of the requested type.</returns>
        /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the type is not registered.</exception>
        object Resolve(Type type);

        /// <summary>
        /// Attempts to resolve an instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <param name="instance">The resolved instance, or null if not registered.</param>
        /// <returns>True if the type was resolved successfully, false otherwise.</returns>
        bool TryResolve<T>(out T instance) where T : class;

        /// <summary>
        /// Creates a child scope with its own lifetime.
        /// </summary>
        /// <returns>A new container scope.</returns>
        /// <remarks>
        /// Scoped registrations are isolated per scope.
        /// Singleton registrations are shared with parent.
        /// </remarks>
        IContainerScope CreateScope();

        /// <summary>
        /// Checks if a type is registered in the container.
        /// </summary>
        /// <typeparam name="T">The type to check.</typeparam>
        /// <returns>True if the type is registered, false otherwise.</returns>
        bool IsRegistered<T>() where T : class;

        /// <summary>
        /// Checks if a type is registered in the container.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is registered, false otherwise.</returns>
        bool IsRegistered(Type type);
    }

    /// <summary>
    /// Represents a scoped container with its own lifetime.
    /// </summary>
    public interface IContainerScope : IContainer
    {
        /// <summary>
        /// Gets the parent container that created this scope.
        /// </summary>
        IContainer Parent { get; }
    }
}
