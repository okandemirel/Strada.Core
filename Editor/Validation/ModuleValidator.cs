using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Validates Strada module structure, naming conventions, and organization.
    /// Checks for required files, assembly definitions, and proper folder structure.
    /// </summary>
    public class ModuleValidator
    {
        /// <summary>
        /// Validates a module at the given path.
        /// </summary>
        public static ValidationResult ValidateModule(string modulePath)
        {
            var result = new ValidationResult();
            var moduleName = Path.GetFileName(modulePath.TrimEnd('/'));

            if (!Directory.Exists(modulePath))
            {
                result.AddError(
                    $"Module directory does not exist: {modulePath}",
                    modulePath,
                    "Module Structure"
                );
                return result;
            }

            ValidateModuleName(result, moduleName, modulePath);
            ValidateFolderStructure(result, modulePath, moduleName);
            ValidateAssemblyDefinitions(result, modulePath, moduleName);
            ValidateModuleInstaller(result, modulePath, moduleName);

            return result;
        }

        /// <summary>
        /// Validates all modules in the project.
        /// </summary>
        public static ValidationResult ValidateAllModules()
        {
            var result = new ValidationResult();
            var modulesPath = "Assets/Modules";

            if (!Directory.Exists(modulesPath))
            {
                result.AddWarning(
                    "No Modules folder found at Assets/Modules",
                    modulesPath,
                    "Module Structure",
                    "Create an Assets/Modules folder for your modules"
                );
                return result;
            }

            var moduleDirs = Directory.GetDirectories(modulesPath);

            if (moduleDirs.Length == 0)
            {
                result.AddInfo("No modules found in Assets/Modules", modulesPath, "Module Structure");
                return result;
            }

            foreach (var moduleDir in moduleDirs)
            {
                var moduleResult = ValidateModule(moduleDir);
                result.Merge(moduleResult);
            }

            result.AddInfo(
                $"Validated {moduleDirs.Length} module(s)",
                modulesPath,
                "Module Structure"
            );

            return result;
        }

        private static void ValidateModuleName(ValidationResult result, string moduleName, string modulePath)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                result.AddError(
                    "Module name is empty",
                    modulePath,
                    "Module Naming"
                );
                return;
            }

            if (moduleName.StartsWith("_"))
            {
                result.AddInfo(
                    $"Module '{moduleName}' is a template (starts with _)",
                    modulePath,
                    "Module Naming"
                );
                return;
            }

            if (!char.IsUpper(moduleName[0]))
            {
                result.AddWarning(
                    $"Module name '{moduleName}' should start with an uppercase letter",
                    modulePath,
                    "Module Naming",
                    "Rename the module to start with an uppercase letter"
                );
            }

            if (moduleName.Contains(" "))
            {
                result.AddError(
                    $"Module name '{moduleName}' contains spaces",
                    modulePath,
                    "Module Naming",
                    "Remove spaces from the module name"
                );
            }
        }

        private static void ValidateFolderStructure(ValidationResult result, string modulePath, string moduleName)
        {
            var scriptsPath = Path.Combine(modulePath, "Scripts");

            if (!Directory.Exists(scriptsPath))
            {
                result.AddError(
                    $"Module '{moduleName}' is missing /Scripts folder",
                    modulePath,
                    "Module Structure",
                    "Create a Scripts folder in the module root"
                );
                return;
            }

            var recommendedFolders = new[]
            {
                "Scripts/Data",
                "Scripts/Data/ValueObjects",
                "Scripts/Data/UnityObjects"
            };

            foreach (var folder in recommendedFolders)
            {
                var folderPath = Path.Combine(modulePath, folder);
                if (!Directory.Exists(folderPath))
                {
                    result.AddInfo(
                        $"Module '{moduleName}' is missing recommended folder: {folder}",
                        modulePath,
                        "Module Structure"
                    );
                }
            }
        }

        private static void ValidateAssemblyDefinitions(ValidationResult result, string modulePath, string moduleName)
        {
            var asmdefFiles = Directory.GetFiles(modulePath, "*.asmdef", SearchOption.TopDirectoryOnly);

            if (asmdefFiles.Length == 0)
            {
                result.AddError(
                    $"Module '{moduleName}' is missing assembly definition",
                    modulePath,
                    "Assembly Definitions",
                    "Create an assembly definition file for the module"
                );
                return;
            }

            var expectedAsmdefName = $"Strada.Modules.{moduleName}.asmdef";
            var hasCorrectAsmdef = asmdefFiles.Any(f => Path.GetFileName(f) == expectedAsmdefName);

            if (!hasCorrectAsmdef)
            {
                result.AddWarning(
                    $"Module '{moduleName}' assembly definition does not follow naming convention",
                    modulePath,
                    "Assembly Definitions",
                    $"Rename assembly definition to {expectedAsmdefName}"
                );
            }

            foreach (var asmdefPath in asmdefFiles)
            {
                ValidateAssemblyDefinitionContent(result, asmdefPath, moduleName);
            }
        }

        private static void ValidateAssemblyDefinitionContent(ValidationResult result, string asmdefPath, string moduleName)
        {
            var json = File.ReadAllText(asmdefPath);

            if (!json.Contains("Strada.Core"))
            {
                result.AddWarning(
                    $"Assembly definition does not reference Strada.Core",
                    asmdefPath,
                    "Assembly Definitions",
                    "Add a reference to Strada.Core in the assembly definition"
                );
            }

            var expectedNamespace = $"Strada.Modules.{moduleName}";
            if (!json.Contains(expectedNamespace))
            {
                result.AddWarning(
                    $"Assembly definition does not use expected namespace: {expectedNamespace}",
                    asmdefPath,
                    "Assembly Definitions",
                    $"Set rootNamespace to {expectedNamespace}"
                );
            }
        }

        private static void ValidateModuleInstaller(ValidationResult result, string modulePath, string moduleName)
        {
            var installerFiles = Directory.GetFiles(modulePath, "*ModuleInstaller.cs", SearchOption.AllDirectories);

            if (installerFiles.Length == 0)
            {
                result.AddWarning(
                    $"Module '{moduleName}' is missing ModuleInstaller",
                    modulePath,
                    "Module Installation",
                    "Create a ModuleInstaller class that implements IModuleInstaller"
                );
                return;
            }

            var expectedInstallerName = $"{moduleName}ModuleInstaller.cs";
            var hasCorrectInstaller = installerFiles.Any(f => Path.GetFileName(f) == expectedInstallerName);

            if (!hasCorrectInstaller)
            {
                result.AddWarning(
                    $"Module installer does not follow naming convention",
                    modulePath,
                    "Module Installation",
                    $"Rename installer to {expectedInstallerName}"
                );
            }

            foreach (var installerPath in installerFiles)
            {
                ValidateInstallerContent(result, installerPath, moduleName);
            }
        }

        private static void ValidateInstallerContent(ValidationResult result, string installerPath, string moduleName)
        {
            var content = File.ReadAllText(installerPath);

            if (!content.Contains("IModuleInstaller"))
            {
                result.AddError(
                    "Module installer does not implement IModuleInstaller",
                    installerPath,
                    "Module Installation",
                    "Add IModuleInstaller interface to the installer class"
                );
            }

            var requiredMethods = new[] { "Install", "Initialize", "Shutdown" };

            foreach (var method in requiredMethods)
            {
                if (!content.Contains(method))
                {
                    result.AddError(
                        $"Module installer is missing {method} method",
                        installerPath,
                        "Module Installation",
                        $"Implement the {method} method from IModuleInstaller"
                    );
                }
            }
        }
    }
}
