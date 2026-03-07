using UnityEngine;

namespace Strada.Core.Sync
{
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

            _viewRegistry.ForceSyncAll();
        }
    }
}
