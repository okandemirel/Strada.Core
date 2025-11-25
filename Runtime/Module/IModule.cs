using System;
using System.Collections.Generic;
using Strada.Core.DI;

namespace Strada.Core.Module
{
    /// <summary>
    /// Defines the contract for a self-contained module within the Strada framework.
    /// Modules are responsible for registering their own services and systems.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Called during world creation to register services with the dependency injection container.
        /// </summary>
        /// <param name="builder">The container builder to register services with.</param>
        void RegisterServices(ContainerBuilder builder);

        /// <summary>
        /// Gets the types of all systems provided by this module.
        /// The SystemExecutor will resolve and instantiate these systems.
        /// </summary>
        /// <returns>A collection of system types.</returns>
        IEnumerable<Type> GetSystemTypes();

        /// <summary>
        /// Called after the world and all modules have been created and their services registered.
        /// Use this to resolve dependencies and perform initialization logic.
        /// </summary>
        /// <param name="container">The fully built container for resolving dependencies.</param>
        void Initialize(IContainer container);

        /// <summary>
        /// Called when the world is being disposed. Use this to clean up resources.
        /// </summary>
        void Shutdown();
    }
}