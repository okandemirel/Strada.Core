using UnityEngine;

namespace Strada.Core.Sync
{
    /// <summary>
    /// Sync mode for ViewSyncRunner.
    /// </summary>
    public enum ViewSyncMode
    {
        /// <summary>
        /// Only sync views with dirty bindings (reactive mode, better performance).
        /// </summary>
        DirtyOnly,

        /// <summary>
        /// Force sync all views every frame (legacy mode, for high-frequency updates like position).
        /// </summary>
        ForceAll,

        /// <summary>
        /// Manual sync mode - no automatic syncing, call Sync() on views manually.
        /// </summary>
        Manual
    }

    /// <summary>
    /// MonoBehaviour that drives view synchronization each frame.
    /// Syncs all registered views with their bound ECS entities.
    /// </summary>
    /// <remarks>
    /// <para>By default, uses DirtyOnly mode which only syncs views with dirty bindings.</para>
    /// <para>For high-frequency updates (like position), use ForceAll mode or call ForceSyncAll() manually.</para>
    /// <para>Set syncMode to Manual to disable automatic syncing entirely.</para>
    /// </remarks>
    public class ViewSyncRunner : MonoBehaviour
    {
        [Tooltip("Sync mode: DirtyOnly (reactive), ForceAll (every frame), or Manual (disabled)")]
        [SerializeField] private ViewSyncMode _syncMode = ViewSyncMode.DirtyOnly;

        private ViewRegistry _viewRegistry;

        /// <summary>
        /// Gets or sets the sync mode.
        /// </summary>
        public ViewSyncMode SyncMode
        {
            get => _syncMode;
            set => _syncMode = value;
        }

        public void Initialize(ViewRegistry viewRegistry)
        {
            _viewRegistry = viewRegistry;
        }

        private void LateUpdate()
        {
            if (_viewRegistry == null) return;

            switch (_syncMode)
            {
                case ViewSyncMode.DirtyOnly:
                    // Only sync views with dirty bindings (better performance)
                    _viewRegistry.SyncAll();
                    break;

                case ViewSyncMode.ForceAll:
                    // Force sync all view bindings (legacy behavior)
                    _viewRegistry.ForceSyncAll();
                    break;

                case ViewSyncMode.Manual:
                    // Do nothing - user will call sync manually
                    break;
            }
        }

        /// <summary>
        /// Manually trigger a sync of all views with dirty bindings.
        /// </summary>
        public void SyncDirty()
        {
            _viewRegistry?.SyncAll();
        }

        /// <summary>
        /// Manually trigger a force sync of all views regardless of dirty flag.
        /// </summary>
        public void ForceSync()
        {
            _viewRegistry?.ForceSyncAll();
        }
    }
}
