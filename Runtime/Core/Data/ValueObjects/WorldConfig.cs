using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strada.Core.Data.ValueObjects
{
    [Serializable]
    public class ModuleData
    {
        [field: SerializeField] public string TypeName { get; set; }
        [field: SerializeField] public bool Enabled { get; set; } = true;
        [field: SerializeField] public int Priority { get; set; }
    }

    [Serializable]
    public class WorldConfig
    {
        [field: SerializeField] public List<ModuleData> Modules { get; set; } = new();
        [field: SerializeField] public bool AutoUpdate { get; set; } = true;
        [field: SerializeField] public bool VerboseLogging { get; set; }

        public bool IsValid()
        {
            return Modules != null;
        }
    }
}
