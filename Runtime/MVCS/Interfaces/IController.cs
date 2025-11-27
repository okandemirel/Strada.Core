namespace Strada.Core.MVCS.Interfaces
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
    }

    /// <summary>
    /// Base interface for controllers that require cleanup.
    /// </summary>
    public interface IDisposableController : IController, System.IDisposable
    {
    }

    /// <summary>
    /// Interface for controllers that need fixed-timestep ticks.
    /// </summary>
    public interface IFixedTickController : IController
    {
        /// <summary>
        /// Ticks the controller logic at a fixed timestep.
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed time elapsed since last fixed tick in seconds.</param>
        void FixedTick(float fixedDeltaTime);
    }
}
