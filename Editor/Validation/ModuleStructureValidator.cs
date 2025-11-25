using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Validation
{
    public static class ModuleStructureValidator
    {
        private static readonly string[] RequiredFolders =
        {
            "Controllers",
            "Data",
            "Interfaces",
            "Systems"
        };

        private static readonly string[] OptionalFolders =
        {
            "Data/UnityObjects",
            "Data/ValueObjects",
            "Views",
            "Services",
            "Commands",
            "Signals",
            "StateMachines"
        };

        [MenuItem("Strada/Validate Module Structure")]
        public static void ValidateAllModules()
        {
            var modulesPath = Path.Combine(Application.dataPath, "Modules");
            if (!Directory.Exists(modulesPath))
            {
                Debug.LogWarning("[Strada] No Modules folder found at Assets/Modules");
                return;
            }

            var modules = Directory.GetDirectories(modulesPath);
            if (modules.Length == 0)
            {
                Debug.Log("[Strada] No modules found in Assets/Modules");
                return;
            }

            var issues = new List<ValidationIssue>();

            foreach (var modulePath in modules)
            {
                ValidateModule(modulePath, issues);
            }

            if (issues.Count == 0)
            {
                Debug.Log($"[Strada] All {modules.Length} modules passed validation ✓");
            }
            else
            {
                Debug.LogWarning($"[Strada] Found {issues.Count} issues in module structure:");
                foreach (var issue in issues)
                {
                    if (issue.IsError)
                        Debug.LogError($"  ERROR: {issue.Message}");
                    else
                        Debug.LogWarning($"  WARNING: {issue.Message}");
                }
            }
        }

        private static void ValidateModule(string modulePath, List<ValidationIssue> issues)
        {
            var moduleName = Path.GetFileName(modulePath);

            if (!moduleName.EndsWith("Module"))
            {
                issues.Add(new ValidationIssue(
                    $"Module folder '{moduleName}' should end with 'Module' (e.g., {moduleName}Module)",
                    true));
            }

            var scriptsPath = Path.Combine(modulePath, "Scripts");
            if (!Directory.Exists(scriptsPath))
            {
                issues.Add(new ValidationIssue(
                    $"Module '{moduleName}' is missing required 'Scripts' folder",
                    true));
                return;
            }

            foreach (var required in RequiredFolders)
            {
                var folderPath = Path.Combine(scriptsPath, required);
                if (!Directory.Exists(folderPath))
                {
                    issues.Add(new ValidationIssue(
                        $"Module '{moduleName}' is missing required folder: Scripts/{required}",
                        true));
                }
            }

            var assemblyDefPath = Path.Combine(scriptsPath, $"{moduleName.Replace("Module", "")}.asmdef");
            if (!File.Exists(assemblyDefPath))
            {
                var altPath = Path.Combine(scriptsPath, $"{moduleName}.asmdef");
                if (!File.Exists(altPath))
                {
                    issues.Add(new ValidationIssue(
                        $"Module '{moduleName}' is missing assembly definition file",
                        false));
                }
            }

            ValidateFileNaming(scriptsPath, moduleName, issues);
        }

        private static void ValidateFileNaming(string scriptsPath, string moduleName, List<ValidationIssue> issues)
        {
            var csFiles = Directory.GetFiles(scriptsPath, "*.cs", SearchOption.AllDirectories);
            var modulePrefix = moduleName.Replace("Module", "");

            foreach (var file in csFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                if (fileName.Contains("MonoBehaviour") && !fileName.StartsWith(modulePrefix))
                {
                    issues.Add(new ValidationIssue(
                        $"MonoBehaviour '{fileName}' in '{moduleName}' should be prefixed with '{modulePrefix}'",
                        false));
                }
            }
        }

        private struct ValidationIssue
        {
            public string Message;
            public bool IsError;

            public ValidationIssue(string message, bool isError)
            {
                Message = message;
                IsError = isError;
            }
        }
    }
}
