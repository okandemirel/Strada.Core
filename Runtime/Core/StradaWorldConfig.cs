using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strada.Core
{
    [Serializable]
    public class ModuleConfig
    {
        [field: SerializeField] public string TypeName { get; set; }
        [field: SerializeField] public bool Enabled { get; set; } = true;
        [field: SerializeField] public int Priority { get; set; }
    }

    [CreateAssetMenu(menuName = "Strada/World Config", fileName = "CD_WorldConfig")]
    public class StradaWorldConfig : ScriptableObject
    {
        [field: SerializeField] public List<ModuleConfig> Modules { get; private set; } = new();
        [field: SerializeField] public bool AutoUpdate { get; private set; } = true;
        [field: SerializeField] public bool VerboseLogging { get; private set; }
    }
}
