using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Strada.Core.Editor.ModuleGenerator.Models;
using Strada.Core.Modules;
using UnityEditor;

namespace Strada.Core.Editor.ModuleGenerator
{
    public static class ModuleDiscovery
    {
        private static readonly string[] ScreenPatterns = { "Screen", "View", "UI", "Panel", "Dialog", "Popup" };
        private static readonly string[] TestPatterns = { "Test", "Tests", "Spec" };

        public static List<ModuleInfoData> DiscoverModules()
        {
            var allModules = new Dictionary<string, ModuleInfoData>();

            DiscoverFromFolderStructureRecursive(allModules, "Assets/Modules", null, 0);
            EnrichFromInstallers(allModules);
            EnrichFromModuleConfigs(allModules);

            var rootModules = allModules.Values
                .Where(m => m.Parent == null)
                .OrderBy(m => m.Name)
                .ToList();

            return rootModules;
        }

        public static List<ModuleInfoData> GetFlatList()
        {
            var result = new List<ModuleInfoData>();
            var rootModules = DiscoverModules();

            foreach (var module in rootModules)
            {
                AddToFlatList(result, module);
            }

            return result;
        }

        private static void AddToFlatList(List<ModuleInfoData> list, ModuleInfoData module)
        {
            list.Add(module);
            foreach (var child in module.SubModules.OrderBy(m => m.Name))
            {
                AddToFlatList(list, child);
            }
        }

        private static void DiscoverFromFolderStructureRecursive(
            Dictionary<string, ModuleInfoData> allModules,
            string searchPath,
            ModuleInfoData parent,
            int depth)
        {
            if (!Directory.Exists(searchPath))
                return;

            var directories = Directory.GetDirectories(searchPath);

            foreach (var dir in directories)
            {
                var folderName = Path.GetFileName(dir);

                if (folderName.StartsWith(".") || folderName == "Editor" || folderName == "Tests" ||
                    folderName == "Scripts" || folderName == "Resources" || folderName == "Prefabs")
                    continue;

                if (IsModuleFolder(folderName))
                {
                    var moduleName = ExtractModuleName(folderName);
                    var moduleType = DetermineModuleType(folderName, parent);

                    var module = new ModuleInfoData
                    {
                        Name = moduleName,
                        Path = dir,
                        Type = moduleType,
                        Parent = parent,
                        Depth = depth,
                        IsExpanded = depth == 0
                    };

                    var key = dir.ToLowerInvariant();
                    allModules[key] = module;

                    if (parent != null)
                    {
                        parent.SubModules.Add(module);
                    }

                    DiscoverFromFolderStructureRecursive(allModules, dir, module, depth + 1);
                }
            }
        }

        private static bool IsModuleFolder(string folderName)
        {
            return folderName.EndsWith("Module") ||
                   folderName.Contains("Module") ||
                   ScreenPatterns.Any(p => folderName.Contains(p) && !folderName.StartsWith("-"));
        }

        private static string ExtractModuleName(string folderName)
        {
            var name = folderName;

            if (name.EndsWith("Module"))
                name = name.Substring(0, name.Length - 6);

            return name;
        }

        private static ModuleType DetermineModuleType(string folderName, ModuleInfoData parent)
        {
            if (TestPatterns.Any(p => folderName.Contains(p)))
                return ModuleType.Test;

            if (ScreenPatterns.Any(p => folderName.Contains(p)))
                return ModuleType.Screen;

            if (parent != null)
                return ModuleType.Sub;

            return ModuleType.Main;
        }

        private static void EnrichFromInstallers(Dictionary<string, ModuleInfoData> allModules)
        {
            var installerType = typeof(IModuleInstaller);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => installerType.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

                    foreach (var type in types)
                    {
                        var name = type.Name.Replace("Module", "");

                        var module = allModules.Values.FirstOrDefault(m =>
                            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                        if (module != null)
                        {
                            module.HasInstaller = true;
                            module.Namespace = type.Namespace;
                        }
                    }
                }
                catch { }
            }
        }

        private static void EnrichFromModuleConfigs(Dictionary<string, ModuleInfoData> allModules)
        {
            var guids = AssetDatabase.FindAssets("t:ModuleConfig");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<ModuleConfig>(path);

                if (config != null)
                {
                    var name = config.name.Replace("ModuleConfig", "").Replace("Module", "");
                    var modulePath = Path.GetDirectoryName(path);

                    var module = allModules.Values.FirstOrDefault(m =>
                        string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        (m.Path != null && modulePath != null && m.Path.Contains(modulePath)));

                    if (module != null)
                    {
                        module.HasModuleConfig = true;
                    }
                }
            }
        }

        public static bool ModuleExists(string moduleName)
        {
            var modules = GetFlatList();
            return modules.Any(m =>
                string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Name + "Module", moduleName + "Module", StringComparison.OrdinalIgnoreCase));
        }

        public static string FindAssemblyForModule(string moduleName)
        {
            var installerType = typeof(IModuleInstaller);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var type = assembly.GetTypes()
                        .FirstOrDefault(t => (t.Name == moduleName || t.Name == moduleName + "Module") &&
                                            installerType.IsAssignableFrom(t));

                    if (type != null)
                        return assembly.GetName().Name;
                }
                catch { }
            }

            return null;
        }
    }
}
