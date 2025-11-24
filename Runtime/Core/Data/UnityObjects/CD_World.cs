using Strada.Core.Data.ValueObjects;
using UnityEngine;

namespace Strada.Core.Data.UnityObjects
{
    [CreateAssetMenu(fileName = "CD_World", menuName = "Strada/World Config")]
    public class CD_World : ScriptableObject
    {
        public WorldConfig Config = new WorldConfig();

        private void OnValidate()
        {
            if (!Config.IsValid())
            {
                Debug.LogWarning($"[CD_World] Invalid configuration in {name}");
            }
        }

        public static CD_World CreateDefault()
        {
            var config = CreateInstance<CD_World>();
            config.Config = new WorldConfig();
            return config;
        }
    }
}
