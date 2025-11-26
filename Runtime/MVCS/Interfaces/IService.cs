namespace Strada.Core.MVCS.Interfaces
{
    /// <summary>
    /// Base interface for all MVCS services.
    /// </summary>
    /// <remarks>
    /// Services represent cross-cutting concerns in the MVCS architecture.
    /// They should:
    /// - Handle system-level functionality (audio, persistence, networking, etc.)
    /// - Be registered as Singletons in the DI container
    /// - Have minimal dependencies on other Services
    /// - Be pure C# classes (no MonoBehaviour)
    /// - Provide interfaces for testability
    ///
    /// Examples:
    /// - IAudioService: Play sounds and music
    /// - ISaveService: Save and load game state
    /// - IInputService: Centralized input handling
    /// - INetworkService: Network communication
    /// </remarks>
    public interface IService
    {
        /// <summary>
        /// Initializes the service.
        /// </summary>
        /// <remarks>
        /// Called after the DI container is built and before game starts.
        /// Use this to set up the service, load resources, subscribe to events, etc.
        /// </remarks>
        void Initialize();

        /// <summary>
        /// Ticks the service logic.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last tick in seconds.</param>
        /// <remarks>
        /// Optional: Not all services need per-frame ticks.
        /// Default implementation does nothing.
        /// </remarks>
        void Tick(float deltaTime)
        {
            // Optional: Default implementation does nothing
        }
    }

    /// <summary>
    /// Interface for services that require cleanup.
    /// </summary>
    public interface IDisposableService : IService, System.IDisposable
    {
    }

    /// <summary>
    /// Interface for services that need initialization priority.
    /// </summary>
    public interface IOrderedService : IService
    {
        /// <summary>
        /// Gets the initialization order.
        /// </summary>
        /// <remarks>
        /// Lower values initialize first.
        /// Default is 0. Services without IOrderedService are initialized last.
        /// </remarks>
        int InitializationOrder { get; }
    }
}
