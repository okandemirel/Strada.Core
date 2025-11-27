using System;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.DataProviders
{
    /// <summary>
    /// Abstract base class for editor data providers with common refresh logic.
    /// Handles Play Mode state changes and automatic refresh intervals.
    /// </summary>
    /// <typeparam name="T">The type of data snapshot this provider returns.</typeparam>
    public abstract class EditorDataProviderBase<T> : IEditorDataProvider<T>, IDisposable where T : class
    {
        private T _cachedData;
        private double _lastRefreshTime;
        private bool _isDisposed;
        
        /// <summary>
        /// Minimum interval between automatic refreshes in seconds.
        /// </summary>
        protected virtual double RefreshInterval => 0.5;

        /// <summary>
        /// Gets whether the data source is currently available.
        /// Override to provide custom availability logic.
        /// </summary>
        public virtual bool IsAvailable => Application.isPlaying;

        /// <summary>
        /// Event raised when the underlying data changes.
        /// </summary>
        public event Action OnDataChanged;

        protected EditorDataProviderBase()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Gets the current data snapshot, using cached data if available and fresh.
        /// </summary>
        public T GetData()
        {
            if (!IsAvailable)
            {
                _cachedData = null;
                return null;
            }

            var currentTime = EditorApplication.timeSinceStartup;
            if (_cachedData == null || (currentTime - _lastRefreshTime) > RefreshInterval)
            {
                RefreshInternal();
            }

            return _cachedData;
        }

        /// <summary>
        /// Forces a refresh of the cached data.
        /// </summary>
        public void Refresh()
        {
            if (!IsAvailable)
            {
                _cachedData = null;
                return;
            }

            RefreshInternal();
        }

        private void RefreshInternal()
        {
            try
            {
                _cachedData = FetchData();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                RaiseDataChanged();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{GetType().Name}] Failed to fetch data: {ex.Message}");
                _cachedData = null;
            }
        }

        /// <summary>
        /// Override to implement the actual data fetching logic.
        /// </summary>
        /// <returns>The fetched data snapshot.</returns>
        protected abstract T FetchData();

        /// <summary>
        /// Raises the OnDataChanged event.
        /// </summary>
        protected void RaiseDataChanged()
        {
            OnDataChanged?.Invoke();
        }

        /// <summary>
        /// Called when Play Mode state changes.
        /// Override to add custom behavior.
        /// </summary>
        protected virtual void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    // Clear cache and refresh when entering play mode
                    _cachedData = null;
                    if (IsAvailable)
                    {
                        RefreshInternal();
                    }
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    // Clear cache when exiting play mode
                    _cachedData = null;
                    RaiseDataChanged();
                    break;
            }
        }

        /// <summary>
        /// Invalidates the cached data, forcing a refresh on next GetData call.
        /// </summary>
        protected void InvalidateCache()
        {
            _cachedData = null;
            _lastRefreshTime = 0;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            OnDispose();
        }

        /// <summary>
        /// Override to add custom disposal logic.
        /// </summary>
        protected virtual void OnDispose() { }
    }
}
