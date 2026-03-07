using UnityEngine;

namespace Strada.Core.Sync
{
    public class ViewSyncRunner : MonoBehaviour
    {
        [Tooltip("Sync mode: DirtyOnly (reactive), ForceAll (every frame), or Manual (disabled)")]
        [SerializeField] private ViewSyncMode _syncMode = ViewSyncMode.ForceAll;

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

            _viewRegistry.ForceSyncAll();
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
