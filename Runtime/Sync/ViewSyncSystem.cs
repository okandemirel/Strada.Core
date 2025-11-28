using System.Runtime.CompilerServices;
using Strada.Core.DI.Attributes;
using Strada.Core.ECS;
using Strada.Core.ECS.Systems;

namespace Strada.Core.Sync
{
    public sealed class ViewSyncSystem : SystemBase
    {
        private ViewRegistry _viewRegistry;

        [Inject]
        public void InjectRegistry(ViewRegistry viewRegistry)
        {
            _viewRegistry = viewRegistry;
        }

        protected override void OnUpdate(float deltaTime)
        {
            SyncViews();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SyncViews()
        {
            if (_viewRegistry == null) return;

            var views = _viewRegistry.AllViews;
            int count = views.Count;

            for (int i = 0; i < count; i++)
            {
                views[i].SyncBindings();
            }
        }
    }
}
