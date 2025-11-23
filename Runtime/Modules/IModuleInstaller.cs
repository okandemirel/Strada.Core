using Strada.Core.DI;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Interface for module installers that register module dependencies with the DI container.
    /// </summary>
    /// <remarks>
    /// Every module must implement an installer to register its services, controllers, and models.
    /// Installers are discovered and executed during the bootstrap phase.
    /// </remarks>
    /// <example>
    /// <code>
    /// public class PlayerModuleInstaller : IModuleInstaller
    /// {
    ///     public void Install(IContainerBuilder builder)
    ///     {
    ///         builder.Register&lt;IPlayerModel, PlayerModel&gt;(Lifetime.Singleton);
    ///         builder.Register&lt;IPlayerController, PlayerController&gt;(Lifetime.Transient);
    ///     }
    ///
    ///     public void Initialize(IContainer container)
    ///     {
    ///         var model = container.Resolve&lt;IPlayerModel&gt;();
    ///         model.Initialize();
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IModuleInstaller
    {
        /// <summary>
        /// Registers module dependencies with the container builder.
        /// </summary>
        /// <param name="builder">The container builder to register dependencies with.</param>
        /// <remarks>
        /// This method is called during the bootstrap phase before the container is built.
        /// Register all services, controllers, models, and factories here.
        /// </remarks>
        void Install(IContainerBuilder builder);

        /// <summary>
        /// Initializes the module after the container is built.
        /// </summary>
        /// <param name="container">The built container for resolving dependencies.</param>
        /// <remarks>
        /// This method is called after all modules have been installed and the container is built.
        /// Use this for post-construction initialization that requires resolved dependencies.
        /// Optional: Leave empty if no initialization is needed.
        /// </remarks>
        void Initialize(IContainer container)
        {
            // Optional: Default implementation does nothing
        }

        /// <summary>
        /// Cleans up the module before shutdown.
        /// </summary>
        /// <remarks>
        /// This method is called during application shutdown in reverse installation order.
        /// Use this to release resources, unsubscribe from events, etc.
        /// Optional: Leave empty if no cleanup is needed.
        /// </remarks>
        void Shutdown()
        {
            // Optional: Default implementation does nothing
        }
    }
}
