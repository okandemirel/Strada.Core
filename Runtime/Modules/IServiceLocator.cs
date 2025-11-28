using System;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Read-only service locator interface for resolving services.
    /// Provides a simplified API for modules to access registered services
    /// without exposing the full DI container.
    /// </summary>
    public interface IServiceLocator
    {
        /// <summary>
        /// Resolves a service of the specified type.
        /// </summary>
        /// <typeparam name="T">The service type to resolve.</typeparam>
        /// <returns>The resolved service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not registered.</exception>
        T Get<T>() where T : class;

        /// <summary>
        /// Resolves a service of the specified runtime type.
        /// </summary>
        /// <param name="serviceType">The service type to resolve.</param>
        /// <returns>The resolved service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not registered.</exception>
        object Get(Type serviceType);

        /// <summary>
        /// Tries to resolve a service of the specified type.
        /// </summary>
        /// <typeparam name="T">The service type to resolve.</typeparam>
        /// <param name="service">The resolved service, or null if not found.</param>
        /// <returns>True if the service was found, false otherwise.</returns>
        bool TryGet<T>(out T service) where T : class;

        /// <summary>
        /// Tries to resolve a service of the specified runtime type.
        /// </summary>
        /// <param name="serviceType">The service type to resolve.</param>
        /// <param name="service">The resolved service, or null if not found.</param>
        /// <returns>True if the service was found, false otherwise.</returns>
        bool TryGet(Type serviceType, out object service);

        /// <summary>
        /// Checks if a service of the specified type is registered.
        /// </summary>
        /// <typeparam name="T">The service type to check.</typeparam>
        /// <returns>True if the service is registered, false otherwise.</returns>
        bool IsRegistered<T>() where T : class;

        /// <summary>
        /// Checks if a service of the specified runtime type is registered.
        /// </summary>
        /// <param name="serviceType">The service type to check.</param>
        /// <returns>True if the service is registered, false otherwise.</returns>
        bool IsRegistered(Type serviceType);
    }
}
