using System.Collections.Generic;
using UnityEngine;

namespace Strada.Core.Editor.ModuleGenerator.Models
{
    /// <summary>
    /// Information about an existing module in the project.
    /// </summary>
    public class ModuleInfoData
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Namespace { get; set; }
        public ModuleType Type { get; set; }
        public bool HasModuleConfig { get; set; }
        public bool HasInstaller { get; set; }
        public ModuleInfoData Parent { get; set; }
        public List<ModuleInfoData> SubModules { get; set; } = new List<ModuleInfoData>();
        public bool IsExpanded { get; set; }
        public int Depth { get; set; }

        public string DisplayName => Type switch
        {
            ModuleType.Main => Name,
            ModuleType.Sub => Name,
            ModuleType.Screen => Name,
            ModuleType.Test => Name,
            _ => Name
        };

        public string TypeLabel => Type switch
        {
            ModuleType.Main => "",
            ModuleType.Sub => "(Sub)",
            ModuleType.Screen => "(Screen)",
            ModuleType.Test => "(Test)",
            _ => ""
        };

        public Color TypeColor => Type switch
        {
            ModuleType.Main => new Color(0.7f, 0.7f, 0.7f),
            ModuleType.Sub => new Color(0.5f, 0.8f, 0.5f),
            ModuleType.Screen => new Color(0.5f, 0.7f, 1.0f),
            ModuleType.Test => new Color(1.0f, 0.7f, 0.4f),
            _ => Color.white
        };

        public bool HasChildren => SubModules != null && SubModules.Count > 0;
    }
}
