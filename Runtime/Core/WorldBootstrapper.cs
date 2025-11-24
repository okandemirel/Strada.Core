using UnityEngine;
using Strada.Core.Data.UnityObjects;

namespace Strada.Core
{
    [DefaultExecutionOrder(-10000)]
    public sealed class WorldBootstrapper : MonoBehaviour
    {
        [SerializeField] private CD_World _worldConfig;

        private World _world;

        private void Awake()
        {
            if (_worldConfig == null)
            {
                Debug.LogError("[WorldBootstrapper] No CD_World assigned!");
                return;
            }

            _world = World.Create(_worldConfig);
            _world.Initialize();

            if (_worldConfig.Config.VerboseLogging)
            {
                Debug.Log("[WorldBootstrapper] World initialized successfully");
            }
        }

        private void Update()
        {
            if (_world != null && _worldConfig.Config.AutoUpdate)
            {
                _world.Update(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            _world?.Dispose();
        }
    }
}
