using System;

namespace Strada.Core.Editor.DataProviders
{
    /// <summary>
    /// Base interface for editor data providers that supply runtime data to editor tools.
    /// </summary>
    /// <typeparam name="T">The type of data snapshot this provider returns.</typeparam>
    public interface IEditorDataProvider<T> where T : class
    {
        /// <summary>
        /// Gets whether the data source is currently available (e.g., in Play Mode).
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets the current data snapshot.
        /// </summary>
        /// <returns>The data snapshot, or null if not available.</returns>
        T GetData();

        /// <summary>
        /// Forces a refresh of the cached data.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Event raised when the underlying data changes.
        /// </summary>
        event Action OnDataChanged;
    }
}
