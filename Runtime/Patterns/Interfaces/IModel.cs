namespace Strada.Core.Patterns.Interfaces
{
    /// <summary>
    /// Base interface for all MVCS models.
    /// </summary>
    /// <remarks>
    /// Models represent the data layer in the MVCS architecture.
    /// They should:
    /// - Contain only data and validation logic
    /// - Have no dependencies on Views or Controllers
    /// - Be easily testable
    /// - Implement INotifyPropertyChanged for reactive UI updates (if needed)
    /// </remarks>
    public interface IModel
    {
        /// <summary>
        /// Initializes the model with default values.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Validates the model's current state.
        /// </summary>
        /// <returns>True if the model is in a valid state, false otherwise.</returns>
        bool Validate();
    }

    /// <summary>
    /// Base interface for models that require cleanup.
    /// </summary>
    public interface IDisposableModel : IModel, System.IDisposable
    {
    }
}
