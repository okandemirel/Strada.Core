using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strada.Core.Core
{
    [Serializable]
    public class ModuleConfig
    {
        [field: SerializeField] public string TypeName { get; set; }
        [field: SerializeField] public bool Enabled { get; set; } = true;
        [field: SerializeField] public int Priority { get; set; }
    }

    [CreateAssetMenu(menuName = "Strada/Core/World Definition", fileName = "WorldDefinition")]
    public class WorldDefinition : ScriptableObject
    {
        [field: SerializeField] public List<ModuleConfig> Modules { get; private set; } = new();
        [field: SerializeField] public bool AutoUpdate { get; private set; } = true;
        [field: SerializeField] public bool VerboseLogging { get; private set; }
    }
}