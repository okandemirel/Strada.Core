namespace Strada.Core.MVCS.Interfaces
{
    /// <summary>
    /// Base interface for all MVCS views.
    /// </summary>
    /// <remarks>
    /// Views represent the presentation layer in the MVCS architecture.
    /// They should:
    /// - Be MonoBehaviour components when Unity integration is needed
    /// - Only handle rendering and user input forwarding
    /// - Have no business logic
    /// - Update based on Model state changes
    /// - Forward user input to Controllers
    /// </remarks>
    public interface IView
    {
        /// <summary>
        /// Initializes the view with its dependencies.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shows the view.
        /// </summary>
        void Show();

        /// <summary>
        /// Hides the view.
        /// </summary>
        void Hide();

        /// <summary>
        /// Gets whether the view is currently visible.
        /// </summary>
        bool IsVisible { get; }
    }

    /// <summary>
    /// Interface for views that require cleanup.
    /// </summary>
    public interface IDisposableView : IView, System.IDisposable
    {
    }

    /// <summary>
    /// Interface for views that display model data.
    /// </summary>
    /// <typeparam name="TModel">The type of model this view displays.</typeparam>
    public interface IView<TModel> : IView where TModel : IModel
    {
        /// <summary>
        /// Updates the view to reflect the current model state.
        /// </summary>
        /// <param name="model">The model containing the data to display.</param>
        void UpdateView(TModel model);
    }
}
