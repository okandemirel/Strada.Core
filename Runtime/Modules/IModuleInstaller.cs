using System;
using Strada.Core.DI;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Interface for module installers that register module dependencies with the DI container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DEPRECATED: This interface is part of the legacy module system.
    /// Please migrate to using ModuleConfig ScriptableObjects for new modules.
    /// </para>
    /// <para>
    /// Migration guide:
    /// 1. Create a new class inheriting from ModuleConfig
    /// 2. Override the Configure(IModuleBuilder) method
    /// 3. Create a ScriptableObject asset and add it to GameBootstrapperConfig
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Legacy approach (deprecated):
    /// public class PlayerModuleInstaller : IModuleInstaller
    /// {
    ///     public void Install(IContainerBuilder builder) { ... }
    ///     public void Initialize(IContainer container) { ... }
    /// }
    ///
    /// // New approach (recommended):
    /// [CreateAssetMenu(menuName = "Modules/Player Module")]
    /// public class PlayerModuleConfig : ModuleConfig
    /// {
    ///     protected override void Configure(IModuleBuilder builder)
    ///     {
    ///         builder.RegisterModel&lt;IPlayerModel, PlayerModel&gt;();
    ///         builder.RegisterController&lt;PlayerController&gt;();
    ///     }
    /// }
    /// </code>
    /// </example>
    [Obsolete("Use ModuleConfig ScriptableObjects with GameBootstrapperConfig instead. See documentation for migration guide.")]
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
        }
    }
}
