namespace Strada.Core.MVCS
{
    /// <summary>
    /// Base interface for all MVCS controllers.
    /// </summary>
    /// <remarks>
    /// Controllers represent the logic layer in the MVCS architecture.
    /// They should:
    /// - Contain business logic and game rules
    /// - Coordinate between Models and Views
    /// - Be pure C# classes (no MonoBehaviour)
    /// - Be injected via the DI container
    /// - Have minimal dependencies on other Controllers
    /// </remarks>
    public interface IController
    {
        /// <summary>
        /// Initializes the controller with its dependencies.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Updates the controller logic.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        void Update(float deltaTime);
    }

    /// <summary>
    /// Base interface for controllers that require cleanup.
    /// </summary>
    public interface IDisposableController : IController, System.IDisposable
    {
    }

    /// <summary>
    /// Interface for controllers that need fixed-timestep updates.
    /// </summary>
    public interface IFixedUpdateController : IController
    {
        /// <summary>
        /// Updates the controller logic at a fixed timestep.
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed time elapsed since last fixed update in seconds.</param>
        void FixedUpdate(float fixedDeltaTime);
    }
}
