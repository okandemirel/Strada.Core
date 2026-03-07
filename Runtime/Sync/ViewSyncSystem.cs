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
            _viewRegistry?.SyncAll();
        }
    }
}
