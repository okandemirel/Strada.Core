using System;
using Strada.Core.DI;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Fluent interface for registering services, systems, and other dependencies within a module.
    /// Provides a VContainer-like API for module configuration.
    /// </summary>
    public interface IModuleBuilder
    {
        /// <summary>
        /// Registers a service with the specified interface and implementation types.
        /// </summary>
        /// <typeparam name="TInterface">The service interface type.</typeparam>
        /// <typeparam name="TImplementation">The service implementation type.</typeparam>
        /// <param name="lifetime">The lifetime of the service.</param>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Singleton)
            where TInterface : class
            where TImplementation : class, TInterface;

        /// <summary>
        /// Registers a concrete type as both interface and implementation.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="lifetime">The lifetime of the service.</param>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder Register<T>(Lifetime lifetime = Lifetime.Singleton) where T : class;

        /// <summary>
        /// Registers a service with runtime types.
        /// </summary>
        /// <param name="interfaceType">The service interface type.</param>
        /// <param name="implementationType">The service implementation type.</param>
        /// <param name="lifetime">The lifetime of the service.</param>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder Register(Type interfaceType, Type implementationType, Lifetime lifetime = Lifetime.Singleton);

        /// <summary>
        /// Registers a pre-created instance as a singleton.
        /// </summary>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <param name="instance">The instance to register.</param>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder RegisterInstance<T>(T instance) where T : class;

        /// <summary>
        /// Registers a factory function for creating instances.
        /// </summary>
        /// <typeparam name="T">The type the factory creates.</typeparam>
        /// <param name="factory">The factory function.</param>
        /// <param name="lifetime">The lifetime of created instances.</param>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder RegisterFactory<T>(Func<IServiceLocator, T> factory, Lifetime lifetime = Lifetime.Singleton)
            where T : class;

        /// <summary>
        /// Registers a model type with singleton lifetime.
        /// </summary>
        /// <typeparam name="TInterface">The model interface type.</typeparam>
        /// <typeparam name="TImplementation">The model implementation type.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder RegisterModel<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface;

        /// <summary>
        /// Registers a controller type with singleton lifetime.
        /// </summary>
        /// <typeparam name="T">The controller type.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder RegisterController<T>() where T : class;

        /// <summary>
        /// Registers a service type with singleton lifetime.
        /// </summary>
        /// <typeparam name="TInterface">The service interface type.</typeparam>
        /// <typeparam name="TImplementation">The service implementation type.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder RegisterService<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface;

        /// <summary>
        /// Registers a factory type with singleton lifetime.
        /// </summary>
        /// <typeparam name="TInterface">The factory interface type.</typeparam>
        /// <typeparam name="TImplementation">The factory implementation type.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IModuleBuilder RegisterFactory<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface;
    }
}
