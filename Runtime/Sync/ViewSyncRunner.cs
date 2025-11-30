using UnityEngine;

namespace Strada.Core.Sync
{
    /// <summary>
    /// MonoBehaviour that drives view synchronization each frame.
    /// Syncs all registered views with their bound ECS entities.
    /// </summary>
    public class ViewSyncRunner : MonoBehaviour
    {
        private ViewRegistry _viewRegistry;

        public void Initialize(ViewRegistry viewRegistry)
        {
            _viewRegistry = viewRegistry;
        }

        private void LateUpdate()
        {
            if (_viewRegistry == null) return;

            // Force sync all view bindings with ECS data (position updates every frame)
            _viewRegistry.ForceSyncAll();
        }
    }
}
